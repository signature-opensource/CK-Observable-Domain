using CK.Core;
using CK.Text;
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
                          bool leaveOpen = false,
                          bool debugMode = false,
                          Encoding? encoding = null,
                          int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( stream == null ) throw new ArgumentNullException( nameof( stream ) );
            CheckDisposed();

            // Since we only need the read lock, whenever multiple threads Save() concurrently,
            // the monitor (of the fake transaction) is at risk. This is why we secure the Save with its own lock: since
            // only one Save at a time can be executed and no other "read with a monitor (even in a fake transaction)" exists.
            // Since this is clearly an edge case, we use a lock with the same timeout and we don't care of a potential 2x wait time.
            if( !Monitor.TryEnter( _saveLock, millisecondsTimeout ) ) return false;

            List<IDestroyable>? destroyedRefList = null;

            void Track( IDestroyable o )
            {
                if( destroyedRefList == null ) destroyedRefList = new List<IDestroyable>();
                Debug.Assert( !destroyedRefList.Contains( o ) );
                destroyedRefList.Add( o );
            }

            using( var w = new BinarySerializer( stream, _serializers, leaveOpen, encoding, Track ) )
            {
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
                        w.WriteSmallInt32( CurrentSerializationVersion );
                        w.DebugWriteMode( debugMode ? (bool?)debugMode : null );
                        w.Write( _currentObjectUniquifier );
                        w.Write( _domainSecret );
                        if( debugMode ) monitor.Trace( $"Domain {DomainName}: Tran #{_transactionSerialNumber} - {_transactionCommitTimeUtc:o}, {_actualObjectCount} objects." );
                        w.Write( DomainName );
                        w.Write( _transactionSerialNumber );
                        w.Write( _transactionCommitTimeUtc );
                        w.Write( _actualObjectCount );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _freeList.Count );
                        foreach( var i in _freeList ) w.WriteNonNegativeSmallInt32( i );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _properties.Count );
                        foreach( var p in _propertiesByIndex )
                        {
                            w.Write( p.PropertyName );
                        }
                        w.DebugWriteSentinel();
                        Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );

                        // First writes the roots: any reachable objects (including
                        // Internal and Timed objects) will be written. Event callbacks' object will
                        // not if they are destroyed => any non saved objects after these roots
                        // are de facto not reachable from the roots.
                        w.WriteNonNegativeSmallInt32( _roots.Count );
                        foreach( var r in _roots )
                        {
                            w.WriteObject( r );
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
                            if( w.WriteObject( o ) && o != null && trackLostObjects )
                            {
                                if( lostObservableObjects == null ) lostObservableObjects = new List<ObservableObject>();
                                lostObservableObjects.Add( o );
                            }
                        }
                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _internalObjectCount );
                        var f = _firstInternalObject;
                        while( f != null )
                        {
                            Debug.Assert( !f.IsDestroyed, "Disposed internal objects are removed from the list." );
                            if( w.WriteObject( f ) && trackLostObjects )
                            {
                                if( lostInternalObjects == null ) lostInternalObjects = new List<InternalObject>();
                                lostInternalObjects.Add( f );
                            }
                            f = f.Next;
                        }
                        w.DebugWriteSentinel();
                        var (lostTimedObjects, unusedPooledReminders, pooledReminderCount) = _timeManager.Save( monitor, w, trackLostObjects );
                        w.DebugWriteSentinel();
                        _sidekickManager.Save( w );
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

        bool DoRealLoad( IActivityMonitor monitor, BinaryDeserializer r, string expectedName, bool? startTimer )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            _deserializeOrInitializing = true;
            try
            {
                UnloadDomain( monitor );
                int version = r.ReadSmallInt32();
                if( version < 5 || version > CurrentSerializationVersion )
                {
                    throw new InvalidDataException( $"Version must be between 5 and {CurrentSerializationVersion}. Version read: {version}." );
                }
                r.SerializationVersion = version;
                r.DebugReadMode();
                _currentObjectUniquifier = r.ReadInt32();
                _domainSecret = r.ReadBytes( DomainSecretKeyLength );
                var loaded = r.ReadString();
                if( loaded != expectedName ) throw new InvalidDataException( $"Domain name mismatch: loading domain named '{loaded}' but expected '{expectedName}'." );

                _transactionSerialNumber = r.ReadInt32();
                _transactionCommitTimeUtc = r.ReadDateTime();
                _actualObjectCount = r.ReadInt32();

                r.DebugCheckSentinel();

                // Clears and read the new free list.
                _freeList.Clear();
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _freeList.Add( r.ReadNonNegativeSmallInt32() );
                }

                r.DebugCheckSentinel();

                // Read the properties index.
                count = r.ReadNonNegativeSmallInt32();
                for( int iProp = 0; iProp < count; iProp++ )
                {
                    string name = r.ReadString();
                    var p = new ObservablePropertyChangedEventArgs( iProp, name );
                    _properties.Add( name, p );
                    _propertiesByIndex.Add( p );
                }

                r.DebugCheckSentinel();

                // Resize _objects array.
                _objectsListCount = count = _actualObjectCount + _freeList.Count;
                while( _objectsListCount > _objects.Length )
                {
                    Array.Resize( ref _objects, _objects.Length * 2 );
                }

                if( version == 5 )
                {
                    // Reads objects. This can read Internal and Timed objects.
                    for( int i = 0; i < count; ++i )
                    {
                        _objects[i] = (ObservableObject?)r.ReadObject();
                        Debug.Assert( _objects[i] == null || !_objects[i].IsDestroyed );
                    }

                    // Fill roots array.
                    r.DebugCheckSentinel();
                    count = r.ReadNonNegativeSmallInt32();
                    while( --count >= 0 )
                    {
                        _roots.Add( _objects[r.ReadNonNegativeSmallInt32()] as ObservableRootObject );
                    }
                }
                else
                {
                    // Reading roots first (including Internal and Timed objects).
                    int rootCount = r.ReadNonNegativeSmallInt32();
                    for( int i = 0; i < rootCount; ++i )
                    {
                        _roots.Add( (ObservableRootObject)r.ReadObject()! );
                    }
                    // Reads all the objects. 
                    for( int i = 0; i < count; ++i )
                    {
                        _objects[i] = (ObservableObject?)r.ReadObject();
                        Debug.Assert( _objects[i] == null || !_objects[i]!.IsDestroyed );
                    }
                }

                // Reading InternalObjects.
                r.DebugCheckSentinel();
                count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    var o = (InternalObject)r.ReadObject()!;
                    Debug.Assert( !o.IsDestroyed );
                    Register( o );
                }
                // Reading Timed events.
                r.DebugCheckSentinel();
                bool timerRunning = _timeManager.Load( monitor, r );
                r.DebugCheckSentinel();
                _sidekickManager.Load( r );
                // This is where specialized typed ObservableDomain bind their roots.
                OnLoaded();
                // Calling PostDeserializationActions finalizes the object's graph.
                r.ImplementationServices.ExecutePostDeserializationActions();

                if( startTimer.HasValue ) timerRunning = startTimer.Value;
                if( !_sidekickManager.CreateWaitingSidekicks( monitor, ex => { }, true ) )
                {
                    var msg = "At least one critical error occurred while activating sidekicks. The error should be investigated since this may well be a blocking error.";
                    if( timerRunning )
                    {
                        timerRunning = false;
                        msg += " The TimeManager (that should have ran) has been stopped.";
                    }
                    monitor.Error( msg );
                }
                return timerRunning;
            }
            finally
            {
                _deserializeOrInitializing = false;
            }
        }

        /// <summary>
        /// Unloads this domain by clearing all internal state: it is ready to be reloaded
        /// or to be forgotten.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        void UnloadDomain( IActivityMonitor monitor )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            // Call Unload( false ) on all objects.
            for( int i = 0; i < _objectsListCount; ++i )
            {
                var o = _objects[i];
                if( o != null )
                {
                    Debug.Assert( !o.IsDestroyed );
                    o.Unload( false );
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
            _sidekickManager.Clear( monitor );

            // Clears any internal objects.
            var internalObj = _firstInternalObject;
            while( internalObj != null )
            {
                internalObj.Unload( false );
                internalObj = internalObj.Next;
            }
            _firstInternalObject = _lastInternalObject = null;
            _internalObjectCount = 0;

            // Clears any time event objects.
            _timeManager.ClearAndStop( monitor );

            // Clears and read the properties index.
            _properties.Clear();
            _propertiesByIndex.Clear();

            _currentObjectUniquifier = 0;
        }
    }
}
