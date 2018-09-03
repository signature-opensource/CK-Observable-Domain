using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Observable
{
    public class ObservableDomain
    {
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

        readonly Dictionary<string,PropInfo> _properties;
        readonly ChangeTracker _changeTracker;
        readonly IDisposable _reentrancyHandler;
        readonly AllCollection _exposedObjects;
        Stack<int> _freeList;

        ObservableObject[] _objects;
        int _objectsListCount;

        int _actualObjectCount;
        Transaction _currentTran;
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

            public IEnumerator<ObservableObject> GetEnumerator() => _d._objects.Where( o => o != null ).GetEnumerator();

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
                public PropChanged Next;
                public PropChanged Prev;

                public long Key => Info.GetObjectPropertyId( Object );

                public PropChanged( ObservableObject o, PropInfo p, object initial, object final )
                {
                    Object = o;
                    Info = p;
                    InitialValue = initial;
                    FinalValue = final;
                }

                public void MoveToFirst( ref PropChanged first, ref PropChanged last )
                {
                    Debug.Assert( first != this );
                    Debug.Assert( Prev != null );
                    if( Next != null )
                    {
                        Next.Prev = Prev;
                    }
                    else
                    {
                        Debug.Assert( last == this );
                        last = Prev;
                    }
                    Prev.Next = Next;
                    Prev = null;
                    Next = first;
                    first = this;
                }
            }

            readonly List<IObservableEvent> _changeEvents;
            readonly HashSet<ObservableObject> _newObjects;
            readonly Dictionary<long, PropChanged> _propChanged;
            PropChanged _firstPropChanged;
            PropChanged _lastPropChanged;

            public ChangeTracker()
            {
                _changeEvents = new List<IObservableEvent>();
                _newObjects = new HashSet<ObservableObject>();
                _propChanged = new Dictionary<long, PropChanged>();
            }

            public IReadOnlyList<IObservableEvent> Commit()
            {
                PropChanged p = _lastPropChanged;
                while( p != null )
                {
                    if( !p.Object.IsDisposed )
                    {
                        _changeEvents.Add( new PropertyChangedEvent( p.Object, p.Info.PropertyId, p.Info.Name, p.FinalValue ) );
                    }
                    p = p.Prev;
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
                _firstPropChanged = _lastPropChanged = null;
            }

            internal bool IsNewObject( ObservableObject o ) => _newObjects.Contains( o );

            internal void OnNewObject( ObservableObject o, int objectId )
            {
                _changeEvents.Add( new NewObjectEvent( o, objectId ) );
                _newObjects.Add( o );
            }

            internal void OnDisposeObject( ObservableObject o )
            {
                if( IsNewObject( o ) )
                {
                    _changeEvents.RemoveAt( _changeEvents.OfType<NewObjectEvent>().IndexOf( e => e.Object == o ) );
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
                if( _firstPropChanged == null )
                {
                    var c = new PropChanged( o, p, before, after );
                    _propChanged.Add( c.Key, c );
                    _firstPropChanged = _lastPropChanged = c;
                }
                else
                {
                    if( _propChanged.TryGetValue( p.GetObjectPropertyId( o ), out var c ) )
                    {
                        c.FinalValue = after;
                        if( _firstPropChanged != c )
                        {
                            c.MoveToFirst( ref _firstPropChanged, ref _lastPropChanged );
                        }
                    }
                    else
                    {
                        c = new PropChanged( o, p, before, after );
                        _firstPropChanged.Prev = c;
                        c.Next = _firstPropChanged;
                        _firstPropChanged = c;
                        _propChanged.Add( c.Key, c );
                    }
                }
            }

            internal void OnListRemoveAt( ObservableObject o, int index )
            {
                _changeEvents.Add( new ListRemoveAtEvent( o, index ) );
            }

            internal void OnCollectionClear( ObservableObject o )
            {
                _changeEvents.Add( new CollectionClearEvent( o ) );
            }

            internal void OnListInsert( ObservableObject o, int index, object item )
            {
                _changeEvents.Add( new ListInsertEvent( o, index, item ) );
            }

            internal void OnCollectionMapSet( ObservableObject o, object key, object value )
            {
                _changeEvents.Add( new CollectionMapSetEvent( o, key, value ) );
            }

            internal void OnCollectionRemoveKey( ObservableObject o, object key )
            {
                _changeEvents.Add( new CollectionRemoveKeyEvent( o, key ) );
            }
        }

        class Transaction : IObservableTransaction
        {
            readonly ObservableDomain _previous;
            readonly ObservableDomain _domain;

            public Transaction( ObservableDomain d )
            {
                _domain = d;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
            }

            public IReadOnlyList<IObservableEvent> Commit()
            {
                CurrentThreadDomain = _previous;
                _domain._currentTran = null;
                var events = _domain._changeTracker.Commit();
                ++_domain._transactionSerialNumber;
                _domain.TransactionManager?.OnTransactionCommit( _domain, events );
                return events;
            }

            public void Dispose()
            {
                if( _domain._currentTran != null )
                {
                    CurrentThreadDomain = null;
                    _domain._currentTran = null;
                    _domain._changeTracker.Reset();
                    // Edge case: very first transaction. We simply reset the
                    // domain.
                    if( _domain._transactionSerialNumber == 0 )
                    {
                        _domain.Reset();
                    }
                    else _domain.TransactionManager?.OnTransactionFailure( _domain );
                }
            }
        }

        class ReetrancyHandler : IDisposable
        {
            readonly ObservableDomain _d;
            public ReetrancyHandler(ObservableDomain d)
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

        public ObservableDomain( IObservableTransactionManager tm, IActivityMonitor monitor )
        {
            DomainNumber = Interlocked.Increment( ref _domainNumber );
            Monitor = monitor ?? new ActivityMonitor( $"Observable Domain n°{DomainNumber}." );
            TransactionManager = tm;
            _objects = new ObservableObject[512];
            _freeList = new Stack<int>();
            _properties = new Dictionary<string, PropInfo>();
            _changeTracker = new ChangeTracker();
            _reentrancyHandler = new ReetrancyHandler( this );
            _exposedObjects = new AllCollection( this );
        }

        void Reset()
        {
            _freeList.Clear();
            _properties.Clear();
            Array.Clear( _objects, 0, _objectsListCount );
            _objectsListCount = 0;
            _actualObjectCount = 0;
        }

        /// <summary>
        /// Gets all the observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: any attempt
        /// to modify one of them will throw.
        /// </summary>
        public IReadOnlyCollection<ObservableObject> AllObjects => _exposedObjects;

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
            return _currentTran;
        }

        /// <summary>
        /// Enables modifications to be done inside a transaction and a try/catch block.
        /// On success, the event list is returned (that may be empty) and on error returns null.
        /// </summary>
        /// <param name="actions">Any action that alters the objects of this domain.</param>
        /// <returns>Null on error, a possibly empty list of events on success.</returns>
        public IReadOnlyList<IObservableEvent> Modify( Action actions )
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
                    return null;
                }
            }
        }

        public void Save( Stream s, bool leaveOpen = false, Encoding encoding = null )
        {
            using( CheckReentrancyOnly() )
            using( var w = new Serializer( s, leaveOpen, encoding ) )
            {
                w.WriteSmallInt32( 0 ); // Version
                w.Write( _actualObjectCount );
                w.WriteSmallInt32( _freeList.Count );
                foreach( var i in _freeList ) w.WriteSmallInt32( i );
                w.WriteSmallInt32( _properties.Count );
                foreach( var kv in _properties )
                {
                    w.Write( kv.Key );
                    Debug.Assert( kv.Key == kv.Value.Name );
                    w.WriteSmallInt32( kv.Value.PropertyId );
                }
                Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );
                foreach( var o in _objects ) w.WriteObject( o );
            }
        }

        public void Load( Stream s, bool leaveOpen = false, Encoding encoding = null )
        {
            using( Monitor.OpenInfo( $"Loading Domain n°{DomainNumber}." ) )
            using( CheckReentrancyOnly() )
            using( var d = new Deserializer( this, s, leaveOpen, encoding ) )
            {
                DoLoad( d );
            }
        }

        void DoLoad( Deserializer d )
        {
            _deserializing = true;
            try
            {
                var r = d.Reader;
                int version = r.ReadSmallInt32();
                _actualObjectCount = r.ReadInt32();

                _freeList.Clear();
                int count = r.ReadSmallInt32();
                while( --count >= 0 )
                {
                    _freeList.Push( r.ReadSmallInt32() );
                }

                _properties.Clear();
                count = r.ReadSmallInt32();
                while( --count >= 0 )
                {
                    string key = r.ReadString();
                    int propertyId = r.ReadSmallInt32();
                    _properties.Add( key, new PropInfo( propertyId, key ) );
                }

                Array.Clear( _objects, 0, _objectsListCount );
                _objectsListCount = count = _actualObjectCount + _freeList.Count;
                while( _objectsListCount > _objects.Length )
                {
                    Array.Resize( ref _objects, _objects.Length * 2 );
                }
                while( --count >= 0 )
                {
                    r.ReadObject<ObservableObject>( x => _objects[x.OId] = x );
                }
                r.ExecuteDeferredActions();
            }
            catch( Exception ex )
            {
                Monitor.Fatal( $"While loading Domain n°{DomainNumber}", ex );
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
            using( CheckTransactionAndReentrancy() )
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
                _changeTracker.OnNewObject( o, idx );
                ++_actualObjectCount;
                return idx;
            }
        }

        internal void Unregister( ObservableObject o )
        {
            Debug.Assert( !o.IsDisposed );
            using( CheckTransactionAndReentrancy() )
            {
                _changeTracker.OnDisposeObject( o );
                _objects[o.OId] = null;
                _freeList.Push( o.OId );
                --_actualObjectCount;
            }
        }

        internal PropertyChangedEventArgs OnPropertyChanged( ObservableObject o, string propertyName, object before, object after )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy() )
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
            }

            return p;
        }

        internal void OnListRemoveAt( ObservableObject o, int index )
        {
            using( CheckTransactionAndReentrancy() )
            {
                _changeTracker.OnListRemoveAt( o, index );
            }
        }

        internal void OnCollectionClear( ObservableObject o )
        {
            using( CheckTransactionAndReentrancy() )
            {
                _changeTracker.OnCollectionClear( o );
            }
        }

        internal void OnListInsert( ObservableObject o, int index, object item )
        {
            using( CheckTransactionAndReentrancy() )
            {
                _changeTracker.OnListInsert( o, index, item );
            }
        }

        internal void OnCollectionMapSet( ObservableObject o, object key, object value )
        {
            using( CheckTransactionAndReentrancy() )
            {
                _changeTracker.OnCollectionMapSet( o, key, value );
            }
        }

        internal void OnCollectionRemoveKey( ObservableObject o, object key )
        {
            using( CheckTransactionAndReentrancy() )
            {
                _changeTracker.OnCollectionRemoveKey( o, key );
            }
        }

        IDisposable CheckTransactionAndReentrancy()
        {
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
