using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CK.Observable
{
    public class ObservableDomain
    {
        /// <summary>
        /// An artificial <see cref="CKExceptionData"/> that is added to
        /// <see cref="IObservableTransaction.Errors"/> whenever a transaction
        /// has not been committed.
        /// </summary>
        public static readonly CKExceptionData UncomittedTransaction = new CKExceptionData( "Uncommitted transaction.", "Not.An.Exception", "Not.An.Exception, No.Assembly", null, null, null, null, null, null );

        static readonly Type[] _observableRootCtorParameters = new Type[] { typeof( ObservableDomain ) };

        class PropInfo
        {
            public readonly PropertyChangedEventArgs EventArg;
            public int PropertyId { get; }
            public string Name => EventArg.PropertyName;

            public PropInfo( int propertyId, string name )
            {
                EventArg = new PropertyChangedEventArgs( name );
                PropertyId = propertyId;
            }

            public long GetObjectPropertyId( ObservableObject o )
            {
                Debug.Assert( o.OId >= 0 );
                long r = o.OId;
                return (r << 24) | (uint)PropertyId;
            }
        }

        static int _domainNumber;

        [ThreadStatic]
        internal static ObservableDomain CurrentThreadDomain;

        internal readonly IExporterResolver _exporters;
        readonly ISerializerResolver _serializers;
        readonly IDeserializerResolver _deserializers;

        /// <summary>
        /// Maps property names to PropInfo that contains the property index.
        /// </summary>
        readonly Dictionary<string, PropInfo> _properties;
        /// <summary>
        /// Map property index to PropInfo that contains the property name.
        /// </summary>
        readonly List<PropInfo> _propertiesByIndex;

        readonly ChangeTracker _changeTracker;
        readonly IDisposable _reentrancyHandler;
        readonly AllCollection _exposedObjects;
        Stack<int> _freeList;

        ObservableObject[] _objects;

        /// <summary>
        /// Since we manage the array directly, this is
        /// the equivalent of a List<ObservableObject>.Count (and _objects.Length
        /// is the capacity):
        /// the null cells (the ones registered in _freeList) are included.
        /// </summary>
        int _objectsListCount;

        /// <summary>
        /// This is the actual number of objects, null cells of _objects are NOT included.
        /// This is greater or equal to _rootObjectCount.
        /// </summary>
        int _actualObjectCount;

        /// <summary>
        /// The actual list of root objects.
        /// </summary>
        List<ObservableRootObject> _roots;

        IObservableTransaction _currentTran;
        int _transactionSerialNumber;
        int _reentrancyFlag;
        bool _deserializing;

        class AllCollection : IReadOnlyCollection<ObservableObject>
        {
            readonly ObservableDomain _d;

            public AllCollection( ObservableDomain d )
            {
                _d = d;
            }

            public int Count => _d._actualObjectCount;

            public IEnumerator<ObservableObject> GetEnumerator() => _d._objects.Take( _d._objectsListCount )
                                                                               .Where( o => o != null )
                                                                               .GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        class ChangeTracker
        {
            class PropChanged
            {
                public readonly ObservableObject Object;
                public readonly PropInfo Info;
                public readonly object InitialValue;
                public object FinalValue;

                public long Key => Info.GetObjectPropertyId( Object );

                public PropChanged( ObservableObject o, PropInfo p, object initial, object final )
                {
                    Object = o;
                    Info = p;
                    InitialValue = initial;
                    FinalValue = final;
                }
            }

            readonly List<ObservableEvent> _changeEvents;
            readonly Dictionary<ObservableObject, List<PropertyInfo>> _newObjects;
            readonly Dictionary<long, PropChanged> _propChanged;

            public ChangeTracker()
            {
                _changeEvents = new List<ObservableEvent>();
                _newObjects = new Dictionary<ObservableObject, List<PropertyInfo>>( PureObjectRefEqualityComparer<ObservableObject>.Default );
                _propChanged = new Dictionary<long, PropChanged>();
            }

            public IReadOnlyList<ObservableEvent> Commit( Func<string, PropInfo> ensurePropertInfo )
            {
                _changeEvents.RemoveAll( e => e is ICollectionEvent c && c.Object.IsDisposed );
                foreach( var p in _propChanged.Values )
                {
                    if( !p.Object.IsDisposed )
                    {
                        _changeEvents.Add( new PropertyChangedEvent( p.Object, p.Info.PropertyId, p.Info.Name, p.FinalValue ) );
                        if( _newObjects.TryGetValue( p.Object, out var exportables ) )
                        {
                            Debug.Assert( exportables != null, "If the object is not exportable, there must be no property changed events." );
                            int idx = exportables.IndexOf( exp => exp.Name == p.Info.Name );
                            if( idx >= 0 ) exportables.RemoveAt( idx );
                        }
                    }
                }
                foreach( var kv in _newObjects )
                {
                    if( kv.Value == null || kv.Value.Count == 0 ) continue;
                    foreach( var exp in kv.Value )
                    {
                        object propValue = exp.GetValue( kv.Key );
                        var pInfo = ensurePropertInfo( exp.Name );
                        _changeEvents.Add( new PropertyChangedEvent( kv.Key, pInfo.PropertyId, pInfo.Name, propValue ) );
                    }
                }
                var result = _changeEvents.ToArray();
                Reset();
                return result;
            }

            public void Reset()
            {
                _changeEvents.Clear();
                _newObjects.Clear();
                _propChanged.Clear();
            }

            internal bool IsNewObject( ObservableObject o ) => _newObjects.ContainsKey( o );

            internal void OnNewObject( ObservableObject o, int objectId, IObjectExportTypeDriver exporter )
            {
                _changeEvents.Add( new NewObjectEvent( o, objectId ) );
                if( exporter != null )
                {
                    _newObjects.Add( o, exporter.ExportableProperties.ToList() );
                }
                else _newObjects.Add( o, null );
            }

            internal void OnDisposeObject( ObservableObject o )
            {
                if( IsNewObject( o ) )
                {
                    int idx = _changeEvents.IndexOf( e => e is NewObjectEvent n ? n.Object == o : false );
                    _changeEvents.RemoveAt( idx );
                    _newObjects.Remove( o );
                }
                else
                {
                    _changeEvents.Add( new DisposedObjectEvent( o ) );
                }
            }

            internal void OnNewProperty( PropInfo info )
            {
                _changeEvents.Add( new NewPropertyEvent( info.PropertyId, info.Name ) );
            }

            internal void OnPropertyChanged( ObservableObject o, PropInfo p, object before, object after )
            {
                PropChanged c;
                if( _propChanged.TryGetValue( p.GetObjectPropertyId( o ), out c ) )
                {
                    c.FinalValue = after;
                }
                else
                {
                    c = new PropChanged( o, p, before, after );
                    _propChanged.Add( c.Key, c );
                }
            }

            internal ListRemoveAtEvent OnListRemoveAt( ObservableObject o, int index )
            {
                var e = new ListRemoveAtEvent( o, index );
                _changeEvents.Add( e );
                return e;
            }

            internal ListSetAtEvent OnListSetAt( ObservableObject o, int index, object value )
            {
                var e = new ListSetAtEvent( o, index, value );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionClearEvent OnCollectionClear( ObservableObject o )
            {
                var e = new CollectionClearEvent( o );
                _changeEvents.Add( e );
                return e;
            }

            internal ListInsertEvent OnListInsert( ObservableObject o, int index, object item )
            {
                var e = new ListInsertEvent( o, index, item );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object value )
            {
                var e = new CollectionMapSetEvent( o, key, value );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionRemoveKeyEvent OnCollectionRemoveKey( ObservableObject o, object key )
            {
                var e = new CollectionRemoveKeyEvent( o, key );
                _changeEvents.Add( e );
                return e;
            }
        }

        class Transaction : IObservableTransaction
        {
            readonly ObservableDomain _previous;
            readonly ObservableDomain _domain;
            CKExceptionData[] _errors;

            public Transaction( ObservableDomain d )
            {
                _domain = d;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
                _errors = Array.Empty<CKExceptionData>();
            }

            public IReadOnlyList<CKExceptionData> Errors => _errors;

            public void AddError( CKExceptionData d )
            {
                Debug.Assert( d != null );
                Array.Resize( ref _errors, _errors.Length + 1 );
                _errors[_errors.Length - 1] = d;
            }

            public IReadOnlyList<ObservableEvent> Commit()
            {
                if( _errors.Length != 0 ) Dispose();
                CurrentThreadDomain = _previous;
                _domain._currentTran = null;
                var events = _domain._changeTracker.Commit( _domain.EnsurePropertyInfo );
                ++_domain._transactionSerialNumber;
                _domain.TransactionManager?.OnTransactionCommit( _domain, DateTime.UtcNow, events );
                return events;
            }

            public void Dispose()
            {
                if( _domain._currentTran != null )
                {
                    if( _errors.Length == 0 ) AddError( UncomittedTransaction );
                    CurrentThreadDomain = _previous;
                    _domain._currentTran = null;
                    _domain._changeTracker.Reset();
                    _domain.TransactionManager?.OnTransactionFailure( _domain, _errors );
                }
            }
        }

        class ReetrancyHandler : IDisposable
        {
            readonly ObservableDomain _d;
            public ReetrancyHandler( ObservableDomain d )
            {
                _d = d;
            }
            public void Dispose()
            {
                Debug.Assert( _d._reentrancyFlag == 1 );
                Interlocked.Exchange( ref _d._reentrancyFlag, 0 );
            }
        }

        public ObservableDomain()
            : this( null, null )
        {
        }

        public ObservableDomain( IActivityMonitor monitor )
            : this( null, monitor )
        {
        }

        public ObservableDomain( IObservableTransactionManager tm )
            : this( tm, null )
        {
        }

        public ObservableDomain(
            IObservableTransactionManager tm,
            IActivityMonitor monitor,
            IExporterResolver exporters = null,
            ISerializerResolver serializers = null,
            IDeserializerResolver deserializers = null )
        {
            DomainNumber = Interlocked.Increment( ref _domainNumber );
            Monitor = monitor ?? new ActivityMonitor( $"Observable Domain n°{DomainNumber}." );
            _exporters = exporters ?? ExporterRegistry.Default;
            _serializers = serializers ?? SerializerRegistry.Default;
            _deserializers = deserializers ?? DeserializerRegistry.Default;
            TransactionManager = tm;
            _objects = new ObservableObject[512];
            _freeList = new Stack<int>();
            _properties = new Dictionary<string, PropInfo>();
            _propertiesByIndex = new List<PropInfo>();
            _changeTracker = new ChangeTracker();
            _reentrancyHandler = new ReetrancyHandler( this );
            _exposedObjects = new AllCollection( this );
            _roots = new List<ObservableRootObject>();
        }

        /// <summary>
        /// Initializes a previously <see cref="Save"/>d domain.
        /// </summary>
        /// <param name="tm">The transaction manager to use. Can be null.</param>
        /// <param name="monitor">The monitor associated to the domain. Can be null (a dedicated one will be created).</param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        public ObservableDomain(
            IObservableTransactionManager tm,
            IActivityMonitor monitor,
            Stream s,
            bool leaveOpen = false,
            Encoding encoding = null )
            : this( tm, monitor )
        {
            Load( s, leaveOpen, encoding );
        }

        class NoTransaction : IObservableTransaction
        {
            public static readonly IObservableTransaction Default = new NoTransaction();

            public void AddError( CKExceptionData d )
            {
            }

            IReadOnlyList<ObservableEvent> IObservableTransaction.Commit() => Array.Empty<ObservableEvent>();

            public IReadOnlyList<CKExceptionData> Errors => Array.Empty<CKExceptionData>();

            void IDisposable.Dispose()
            {
            }
        }

        /// <summary>
        /// This must be called only from <see cref="ObservableDomain{T}"/> constructors.
        /// No event are collected: this is the initial state of the domain.
        /// </summary>
        /// <typeparam name="T">The root type.</typeparam>
        /// <returns>The instance.</returns>
        protected T AddRoot<T>() where T : ObservableRootObject
        {
            _currentTran = NoTransaction.Default;
            var previous = CurrentThreadDomain;
            CurrentThreadDomain = this;
            _deserializing = true;
            try
            {
                var o = (T)typeof( T ).GetConstructor( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                       null,
                                                       _observableRootCtorParameters,
                                                       null ).Invoke( new[] { this } );
                _roots.Add( o );
                return o;
            }
            finally
            {
                _deserializing = false;
                CurrentThreadDomain = previous;
                _currentTran = null;
            }
        }

        /// <summary>
        /// Gets all the observable objects that this domain contains (roots included).
        /// These exposed objects are out of any transactions or reentrancy checks: any attempt
        /// to modify one of them will throw.
        /// </summary>
        public IReadOnlyCollection<ObservableObject> AllObjects => _exposedObjects;

        /// <summary>
        /// Gets the root observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: any attempt
        /// to modify one of them will throw.
        /// </summary>
        public IReadOnlyList<ObservableRootObject> AllRoots => _roots;

        /// <summary>
        /// Gets the current transaction number.
        /// Incremented each time a transaction successfuly ended.
        /// </summary>
        public int TransactionSerialNumber => _transactionSerialNumber;

        /// <summary>
        /// Unique incrmental number for each domain in the AppDomain.
        /// </summary>
        public int DomainNumber { get; }

        /// <summary>
        /// Gets the monitor that is bound to this domain.
        /// </summary>
        public IActivityMonitor Monitor { get; }

        /// <summary>
        /// Gets the associated transaction manager.
        /// </summary>
        public IObservableTransactionManager TransactionManager { get; }

        /// <summary>
        /// Starts a new transaction that must be <see cref="IObservableTransaction.Commit"/>, otherwise
        /// all changes are cancelled.
        /// This must not be called twice (without disposing or committing the existing one) otherwise
        /// an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <returns></returns>
        public IObservableTransaction BeginTransaction()
        {
            if( _currentTran != null ) throw new InvalidOperationException( $"A transaction is already opened for this ObservableDomain n°{DomainNumber}." );
            _currentTran = new Transaction( this );
            TransactionManager?.OnTransactionStart( this, DateTime.UtcNow );
            return _currentTran;
        }

        /// <summary>
        /// Enables modifications to be done inside a transaction and a try/catch block.
        /// On success, the event list is returned (that may be empty) and on error returns null.
        /// </summary>
        /// <param name="actions">Any action that can alter the objects of this domain.</param>
        /// <returns>Null on error, a possibly empty list of events on success.</returns>
        public IReadOnlyList<ObservableEvent> Modify( Action actions )
        {
            using( var t = BeginTransaction() )
            {
                try
                {
                    actions();
                    return t.Commit();
                }
                catch( Exception ex )
                {
                    Monitor.Error( ex );
                    t.AddError( CKExceptionData.CreateFrom( ex ) );
                    return null;
                }
            }
        }

        /// <summary>
        /// Exports this domain as a JSON object with the <see cref="TransactionSerialNumber"/>,
        /// the property name mappings, and the object graph itself that is compatible
        /// with @Signature/json-graph-serialization package and requires a post processing to lift
        /// container (map, list and set) contents.
        /// </summary>
        /// <param name="w">The text writer.</param>
        public void Export( TextWriter w )
        {
            using( CheckReentrancyOnly() )
            {
                var target = new JSONExportTarget( w );

                target.EmitStartObject( -1, ObjectExportedKind.Object );
                target.EmitPropertyName( "N" );
                target.EmitInt32( _transactionSerialNumber );
                target.EmitPropertyName( "C" );
                target.EmitInt32( _actualObjectCount );
                target.EmitPropertyName( "P" );
                target.EmitStartObject( -1, ObjectExportedKind.List );
                foreach( var p in _properties )
                {
                    target.EmitString( p.Value.Name );
                }
                target.EmitEndObject( -1, ObjectExportedKind.List );

                target.EmitPropertyName( "O" );
                ObjectExporter.ExportRootList( target, _objects.Take( _objectsListCount ), _exporters );

                target.EmitPropertyName( "R" );
                target.EmitStartObject( -1, ObjectExportedKind.List );
                foreach( var r in _roots )
                {
                    target.EmitInt32( r.OId );
                }
                target.EmitEndObject( -1, ObjectExportedKind.List );

                target.EmitEndObject( -1, ObjectExportedKind.Object );
            }
        }

        public string ExportToString()
        {
            var w = new StringWriter();
            Export( w );
            return w.ToString();
        }

        /// <summary>
        /// Saves <see cref="AllObjects"/> of this domain.
        /// </summary>
        /// <param name="s">The output stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        public void Save( Stream s, bool leaveOpen = false, Encoding encoding = null )
        {
            using( CheckReentrancyOnly() )
            using( var w = new BinarySerializer( s, _serializers, leaveOpen, encoding ) )
            {
                w.WriteSmallInt32( 0 ); // Version
                w.Write( _transactionSerialNumber );
                w.Write( _actualObjectCount );
                w.WriteNonNegativeSmallInt32( _freeList.Count );
                foreach( var i in _freeList ) w.WriteNonNegativeSmallInt32( i );
                w.WriteNonNegativeSmallInt32( _properties.Count );
                foreach( var p in _propertiesByIndex )
                {
                    w.Write( p.Name );
                }
                Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );
                for( int i = 0; i < _objectsListCount; ++i )
                {
                    w.WriteObject( _objects[i] );
                }
                w.WriteNonNegativeSmallInt32( _roots.Count );
                foreach( var r in _roots ) w.WriteNonNegativeSmallInt32( r.OId );
            }
        }

        /// <summary>
        /// Loads previously <see cref="Save"/>d objects into this domain.
        /// </summary>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        public void Load( Stream s, bool leaveOpen = false, Encoding encoding = null )
        {
            using( Monitor.OpenInfo( $"Loading Domain n°{DomainNumber}." ) )
            using( CheckReentrancyOnly() )
            using( var d = new BinaryDeserializer( s, null, _deserializers, leaveOpen, encoding ) )
            {
                d.Services.Add( this );
                DoLoad( d );
            }
        }

        void DoLoad( BinaryDeserializer r )
        {
            _deserializing = true;
            try
            {
                int version = r.ReadSmallInt32();
                _transactionSerialNumber = r.ReadInt32();
                _actualObjectCount = r.ReadInt32();

                _freeList.Clear();
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _freeList.Push( r.ReadNonNegativeSmallInt32() );
                }

                _properties.Clear();
                _propertiesByIndex.Clear();
                count = r.ReadNonNegativeSmallInt32();
                for( int iProp = 0; iProp < count; iProp++ )
                {
                    string name = r.ReadString();
                    var p = new PropInfo( iProp, name );
                    _properties.Add( name, p );
                    _propertiesByIndex.Add( p );
                }

                Array.Clear( _objects, 0, _objectsListCount );
                _objectsListCount = count = _actualObjectCount + _freeList.Count;
                while( _objectsListCount > _objects.Length )
                {
                    Array.Resize( ref _objects, _objects.Length * 2 );
                }
                for( int i = 0; i < count; ++i )
                {
                    _objects[i] = (ObservableObject)r.ReadObject();
                }
                r.ImplementationServices.ExecutePostDeserializationActions();
                _roots.Clear();
                count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _roots.Add( _objects[r.ReadNonNegativeSmallInt32()] as ObservableRootObject );
                }
            }
            finally
            {
                _deserializing = false;
            }
        }

        /// <summary>
        /// Gets the active domain on the current thread (the last one for which a <see cref="BeginTransaction"/>
        /// has been done an not yet disposed) or throws an <see cref="InvalidOperationException"/> if there is none.
        /// </summary>
        /// <returns>The current domain.</returns>
        internal static ObservableDomain GetCurrentActiveDomain()
        {
            if( CurrentThreadDomain == null )
            {
                throw new InvalidOperationException( "ObservableObject can be created only inside a ObservableDomain transaction." );
            }
            return CurrentThreadDomain;
        }

        internal bool IsDeserializing => _deserializing;

        internal int Register( ObservableObject o )
        {
            using( CheckTransactionAndReentrancy( o ) )
            {
                Debug.Assert( o != null && o.Domain == this );
                int idx;
                if( _freeList.Count > 0 )
                {
                    idx = _freeList.Pop();
                }
                else
                {
                    idx = _objectsListCount++;
                    if( idx == _objects.Length )
                    {
                        Array.Resize( ref _objects, idx * 2 );
                    }
                }
                _objects[idx] = o;
                if( !_deserializing )
                {
                    _changeTracker.OnNewObject( o, idx, o._exporter );
                }
                ++_actualObjectCount;
                return idx;
            }
        }

        internal void Unregister( ObservableObject o )
        {
            Debug.Assert( !o.IsDisposed );
            using( CheckTransactionAndReentrancy( o ) )
            {
                if( !_deserializing ) _changeTracker.OnDisposeObject( o );
                _objects[o.OId] = null;
                _freeList.Push( o.OId );
                --_actualObjectCount;
            }
        }

        internal PropertyChangedEventArgs OnPropertyChanged( ObservableObject o, string propertyName, object before, object after )
        {
            if( _deserializing
                || o._exporter == null
                || !o._exporter.ExportableProperties.Any( p => p.Name == propertyName ) )
            {
                return null;
            }
            using( CheckTransactionAndReentrancy( o ) )
            {
                PropInfo p = EnsurePropertyInfo( propertyName );
                _changeTracker.OnPropertyChanged( o, p, before, after );
                return p.EventArg;
            }
        }

        PropInfo EnsurePropertyInfo( string propertyName )
        {
            if( !_properties.TryGetValue( propertyName, out var p ) )
            {
                p = new PropInfo( _properties.Count, propertyName );
                _changeTracker.OnNewProperty( p );
                _properties.Add( propertyName, p );
                _propertiesByIndex.Add( p );
            }

            return p;
        }

        internal ListRemoveAtEvent OnListRemoveAt( ObservableObject o, int index )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy( o ) )
            {
                return _changeTracker.OnListRemoveAt( o, index );
            }
        }

        internal ListSetAtEvent OnListSetAt( ObservableObject o, int index, object value )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy( o ) )
            {
                return _changeTracker.OnListSetAt( o, index, value );
            }
        }

        internal CollectionClearEvent OnCollectionClear( ObservableObject o )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy( o ) )
            {
                return _changeTracker.OnCollectionClear( o );
            }
        }

        internal ListInsertEvent OnListInsert( ObservableObject o, int index, object item )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy( o ) )
            {
                return _changeTracker.OnListInsert( o, index, item );
            }
        }

        internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object value )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy( o ) )
            {
                return _changeTracker.OnCollectionMapSet( o, key, value );
            }
        }

        internal CollectionRemoveKeyEvent OnCollectionRemoveKey( ObservableObject o, object key )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy( o ) )
            {
                return _changeTracker.OnCollectionRemoveKey( o, key );
            }
        }

        IDisposable CheckTransactionAndReentrancy( ObservableObject o )
        {
            if( o.IsDisposed ) throw new ObjectDisposedException( o.GetType().FullName );
            if( _currentTran == null ) throw new InvalidOperationException( "A transaction is required." );
            if( Interlocked.CompareExchange( ref _reentrancyFlag, 1, 0 ) == 1 ) throw new InvalidOperationException( "Reentrancy detected." );
            return _reentrancyHandler;
        }

        IDisposable CheckReentrancyOnly()
        {
            if( Interlocked.CompareExchange( ref _reentrancyFlag, 1, 0 ) == 1 ) throw new InvalidOperationException( "Reentrancy detected." );
            return _reentrancyHandler;
        }
    }
}
