using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{

    public partial class ObservableDomain
    {
        /// <inheritdoc/>
        public bool Save( IActivityMonitor monitor,
                          Stream stream,
                          bool debugMode = false,
                          int millisecondsTimeout = -1 )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( stream );
            CheckDisposed();

            // Since we only need the read lock, whenever multiple threads Save() concurrently,
            // the monitor (of the fake transaction) is at risk. This is why we secure the Save with its own lock: since
            // only one Save at a time can be executed and no other "read with a monitor (even in a fake transaction)" exists.
            // Since this is clearly an edge case, we use a lock with the same timeout and we don't care of a potential 2x wait time.
            if( !Monitor.TryEnter( _saveLock, millisecondsTimeout ) ) return false;

            List<BinarySerialization.IDestroyable>? destroyedRefList = null;

            void Track( BinarySerialization.IDestroyable o )
            {
                if( destroyedRefList == null ) destroyedRefList = new List<BinarySerialization.IDestroyable>();
                Debug.Assert( !destroyedRefList.Contains( o ) );
                destroyedRefList.Add( o );
            }

            using( var s = BinarySerializer.Create( stream, _serializerContext ) )
            {
                s.OnDestroyedObject += Track;
                bool isWrite = _lock.IsWriteLockHeld;
                bool isRead = _lock.IsReadLockHeld;
                if( !isWrite && !isRead && !_lock.TryEnterReadLock( millisecondsTimeout ) )
                {
                    Monitor.Exit( _saveLock );
                    return false;
                }
                bool needFakeTran = _currentTran == null || _currentTran.Monitor != monitor;
                if( needFakeTran ) new InitializationTransaction( monitor, this, false );
                try
                {
                    using( monitor.OpenInfo( $"Saving domain ({_actualObjectCount} objects, {_internalObjectCount} internals, {_timeManager.AllObservableTimedEvents.Count} timed events)." ) )
                    {
                        s.Writer.Write( (byte)0 ); // Version
                        s.DebugWriteMode( debugMode ? (bool?)debugMode : null );
                        s.Writer.Write( _currentObjectUniquifier );
                        s.Writer.Write( _domainSecret );
                        if( debugMode ) monitor.Trace( $"Domain {DomainName}: Tran #{_transactionSerialNumber} - {_transactionCommitTimeUtc:o}, {_actualObjectCount} objects." );
                        s.Writer.Write( DomainName );
                        s.Writer.Write( _transactionSerialNumber );
                        s.Writer.Write( _transactionCommitTimeUtc );
                        s.Writer.Write( _actualObjectCount );

                        s.DebugWriteSentinel();
                        s.Writer.WriteNonNegativeSmallInt32( _freeList.Count );
                        foreach( var i in _freeList ) s.Writer.WriteNonNegativeSmallInt32( i );

                        s.DebugWriteSentinel();
                        s.Writer.WriteNonNegativeSmallInt32( _properties.Count );
                        foreach( var p in _propertiesByIndex )
                        {
                            s.Writer.Write( p.PropertyName );
                        }
                        s.DebugWriteSentinel();
                        Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );

                        // First writes the roots: any reachable objects (including
                        // Internal and Timed objects) will be written. Event callbacks' object will
                        // not if they are destroyed => any non saved objects after these roots
                        // are de facto not reachable from the roots.
                        s.Writer.WriteNonNegativeSmallInt32( _roots.Count );
                        foreach( var r in _roots )
                        {
                            s.WriteObject( r );
                        }
                        // The tracking list of non reachable objects from roots.
                        bool trackLostObjects = _roots.Count > 0;
                        List<ObservableObject>? lostObservableObjects = null;
                        List<InternalObject>? lostInternalObjects = null;
                        // Then writes all the Observable objects: track the non reachable
                        // objects if we have at least one root.
                        for( int i = 0; i < _objectsListCount; ++i )
                        {
                            var o = _objects[i];
                            Debug.Assert( o == null || !o.IsDestroyed, "Either it is a free cell (that appears in the free list) or the object is NOT disposed." );
                            if( s.WriteNullableObject( o ) && o != null && trackLostObjects )
                            {
                                if( lostObservableObjects == null ) lostObservableObjects = new List<ObservableObject>();
                                lostObservableObjects.Add( o );
                            }
                        }
                        s.DebugWriteSentinel();
                        s.Writer.WriteNonNegativeSmallInt32( _internalObjectCount );
                        var f = _firstInternalObject;
                        while( f != null )
                        {
                            Debug.Assert( !f.IsDestroyed, "Disposed internal objects are removed from the list." );
                            if( s.WriteObject( f ) && trackLostObjects )
                            {
                                if( lostInternalObjects == null ) lostInternalObjects = new List<InternalObject>();
                                lostInternalObjects.Add( f );
                            }
                            f = f.Next;
                        }
                        s.DebugWriteSentinel();
                        var (lostTimedObjects, unusedPooledReminders, pooledReminderCount) = _timeManager.Save( monitor, s, trackLostObjects );
                        s.DebugWriteSentinel();
                        _sidekickManager.Save( s );
                        var data = new LostObjectTracker( this,
                                                          lostObservableObjects,
                                                          lostInternalObjects,
                                                          lostTimedObjects,
                                                          destroyedRefList,
                                                          unusedPooledReminders,
                                                          pooledReminderCount );
                        if( data.HasIssues ) data.DumpLog( monitor );
                        CurrentLostObjectTracker = data;
                        return true;
                    }
                }
                finally
                {
                    if( needFakeTran )
                    {
                        Debug.Assert( _currentTran != null );
                        _currentTran.Dispose();
                    }
                    if( !isWrite && !isRead ) _lock.ExitReadLock();
                    Monitor.Exit( _saveLock );
                }
            }
        }

        bool DoRealLoad( IActivityMonitor monitor, RewindableStream s, string expectedName, bool? startTimer )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            _deserializeOrInitializing = true;
            var prevInitializingStatus = _transactionStatus;
            try
            {
                var r = BinaryDeserializer.Deserialize( s, _deserializerContext, d => DeserializeAndGetTimerState( monitor, expectedName, d ) );
                // Throw on error.
                bool timerRunning = r.GetResult();
                if( startTimer.HasValue ) timerRunning = startTimer.Value;
                // If we are in a real transaction, we leave the deserialization/initialization mode
                // and instantiate any expected sidekick.
                Debug.Assert( _currentTran is Transaction || _currentTran is InitializationTransaction );
                if( _currentTran is Transaction && _sidekickManager.HasWaitingSidekick )
                {
                    // The sidekicks will initialize themselves in a "normal" context. Events will be available in the domain
                    // but will not exported.
                    // and the ObservableEvents will be available.
                    _deserializeOrInitializing = false;
                    if( !_sidekickManager.CreateWaitingSidekicks( monitor, Util.ActionVoid, true ) )
                    {
                        var msg = "At least one critical error occurred while activating sidekicks. The error should be investigated since this may well be a blocking error.";
                        if( timerRunning )
                        {
                            timerRunning = false;
                            msg += " The TimeManager (that should have ran) has been stopped.";
                        }
                        monitor.Error( msg );
                    }
                }
                return timerRunning;
            }
            finally
            {
                _deserializeOrInitializing = false;
                _transactionStatus = prevInitializingStatus;
            }
        }

        bool DeserializeAndGetTimerState( IActivityMonitor monitor, string expectedName, IBinaryDeserializer d )
        {
            bool firstPass = !d.StreamInfo.SecondPass;

            // This is where specialized typed ObservableDomain bind their roots:
            // this must be called before any PostActions added by the objects.
            d.PostActions.Add( BindRoots );

            d.Reader.ReadByte(); // Local version.
            d.DebugReadMode();
            if( firstPass )
            {
                var currentObjectUniquifier = d.Reader.ReadInt32();
                var domainSecret = d.Reader.ReadBytes( DomainSecretKeyLength );
                var loaded = d.Reader.ReadString();
                if( loaded != expectedName ) Throw.InvalidDataException( $"Domain name mismatch: loading domain named '{loaded}' but expected '{expectedName}'." );

                Debug.Assert( _transactionStatus is CurrentTransactionStatus.Instantiating or CurrentTransactionStatus.Deserializing or CurrentTransactionStatus.None );
                // Either we come from the domain's deserialization constructor (Deserializing), or from a constructor with a client.OnDomainCreated that wants to
                // load this domain (Instantiating) or none of this (None):
                //  - This can be a direct call of the Load() methods.
                //  - This can be a Load from a client that rollbacks the current transaction because it is on error.
                // To keep things and the DomainInitializingStatus as simple as possible:
                //  - Instantiating and None transition to Deserializing (because we ARE deserializing!).
                //  - If we are deserializing the same number: it's a RollingBack.
                //  - If we are deserializing an older version: it's a DangerousRollingback.
                //  - If we are deserializing a newer version, no change, it's Deserializing.
                //
                // This works in all cases because the very first transaction number that can be saved is 1
                // and _transactionSerialNumber is let to 0 by the constructors (Instantiating will always be Deserializing).
                // And it makes perfect sense that RollingBack/DangerousRollingback/Deserializing also apply to the explicit
                // call to Load() on an existing domain.
                //
                var tNumber = d.Reader.ReadInt32();
                var tTime = d.Reader.ReadDateTime();
                if( _transactionSerialNumber == tNumber ) _transactionStatus = CurrentTransactionStatus.Rollingback;
                else if( _transactionSerialNumber > tNumber ) _transactionStatus = CurrentTransactionStatus.DangerousRollingback;
                else _transactionStatus = CurrentTransactionStatus.Deserializing;

                _transactionSerialNumber = tNumber;
                _transactionCommitTimeUtc = tTime;
                _domainSecret = domainSecret;

                // We call unload on the current objects only on the first pass and
                // the InitializingStatus is up to date.
                UnloadDomain( monitor, true );

                Debug.Assert( _transactionSerialNumber == tNumber
                              && _transactionCommitTimeUtc == tTime
                              && _domainSecret.AsSpan().SequenceEqual( domainSecret ), "OnUnload doesn't change these." );
                _currentObjectUniquifier = currentObjectUniquifier;
            }
            else
            {
                // On the second pass (if it happens), we just forget all the result of the first pass.
                UnloadDomain( monitor, false );
                _currentObjectUniquifier = d.Reader.ReadInt32();
                // Skips domain secret, domain name, transaction number and transaction date: they have
                // been initialized (or checked for the name) by first pass and are not modified by UnloadDomain.
                d.Reader.ReadBytes( DomainSecretKeyLength );
                d.Reader.ReadString();
                d.Reader.ReadInt32();
                d.Reader.ReadDateTime();
            }

            _actualObjectCount = d.Reader.ReadInt32();

            d.DebugCheckSentinel();

            // Read the new free list.
            Debug.Assert( _freeList.Count == 0 );
            int count = d.Reader.ReadNonNegativeSmallInt32();
            while( --count >= 0 )
            {
                _freeList.Add( d.Reader.ReadNonNegativeSmallInt32() );
            }

            d.DebugCheckSentinel();

            // Read the properties index.
            count = d.Reader.ReadNonNegativeSmallInt32();
            for( int iProp = 0; iProp < count; iProp++ )
            {
                string name = d.Reader.ReadString();
                var p = new ObservablePropertyChangedEventArgs( iProp, name );
                _properties.Add( name, p );
                _propertiesByIndex.Add( p );
            }

            d.DebugCheckSentinel();

            // Resize _objects array.
            _objectsListCount = count = _actualObjectCount + _freeList.Count;
            while( _objectsListCount > _objects.Length )
            {
                Array.Resize( ref _objects, _objects.Length * 2 );
            }

            // Reading roots first (including Internal and Timed objects).
            int rootCount = d.Reader.ReadNonNegativeSmallInt32();
            for( int i = 0; i < rootCount; ++i )
            {
                _roots.Add( d.ReadObject<ObservableRootObject>() );
            }
            // Reads all the objects. 
            for( int i = 0; i < count; ++i )
            {
                var o = d.ReadNullableObject<ObservableObject>();
                Debug.Assert( o == null || !o.IsDestroyed );
                if( o != null && o.OId.Index != i )
                {
                    Throw.InvalidDataException( $"Deserialization error for '{o.GetType().ToCSharpName()}': its deserialization constructor must call \": base( Sliced.{nameof(Sliced.Instance)} )\"." );
                }
                _objects[i] = o;
            }

            // Reading InternalObjects.
            d.DebugCheckSentinel();
            count = d.Reader.ReadNonNegativeSmallInt32();
            while( --count >= 0 )
            {
                var o = d.ReadObject<InternalObject>();
                Debug.Assert( !o.IsDestroyed );
                Register( o );
            }
            // Reading Timed events.
            d.DebugCheckSentinel();
            bool timerRunning = _timeManager.Load( monitor, d );
            d.DebugCheckSentinel();
            _sidekickManager.Load( d );
            return timerRunning;
        }

        /// <summary>
        /// Unloads this domain by clearing all internal state: it is ready to be reloaded
        /// or to be forgotten.
        /// The CurrentTransactionStatus is up to date when this method is called.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="callUnload">Whether <see cref="ObservableObject.Unload(bool)"/> and
        /// <see cref="InternalObject.Unload(bool)"/> must be called.</param>
        void UnloadDomain( IActivityMonitor monitor, bool callUnload )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            Debug.Assert( callUnload || _sidekickManager.IsEmpty, "callUnload == false => sidekicks have already been unloaded." );

            using( monitor.OpenTrace( $"Unloading domain '{DomainName}'{(callUnload ? "" : " NOT")} calling OnUnlaod methods." ) )
            {
                if( callUnload )
                {
                    _sidekickManager.OnUnload( monitor );

                    // Call Unload( gc: false ) on all objects.
                    for( int i = 0; i < _objectsListCount; ++i )
                    {
                        var o = _objects[i];
                        if( o != null )
                        {
                            Debug.Assert( !o.IsDestroyed );
                            o.Unload( gc: false );
                        }
                    }
                }
                // Empty _objects completely.
                Array.Clear( _objects, 0, _objectsListCount );
                _objectsListCount = 0;
                _actualObjectCount = 0;

                // Clears root list.
                _roots.Clear();

                // Free sidekicks and IObservableDomainActionTracker.
                _trackers.Clear();

                // Clears any internal objects.
                if( callUnload )
                {
                    var internalObj = _firstInternalObject;
                    while( internalObj != null )
                    {
                        internalObj.Unload( gc: false );
                        internalObj = internalObj.Next;
                    }
                }
                _firstInternalObject = _lastInternalObject = null;
                _internalObjectCount = 0;

                // Clears any time event objects.
                _timeManager.ClearAndStop( monitor );

                // Clears and read the properties index.
                _properties.Clear();
                _propertiesByIndex.Clear();

                // Clears the free list.
                _freeList.Clear();

                _currentObjectUniquifier = 0;
            }
        }
    }
}
