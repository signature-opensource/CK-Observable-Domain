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
            public readonly int PropertyId;
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

        readonly List<ObservableObject> _objects;
        readonly Dictionary<string,PropInfo> _properties;
        readonly ChangeTracker _changeTracker;
        readonly IDisposable _reentrancyHandler;
        readonly AllCollection _exposedObjects;
        Stack<int> _freeList;

        int _objectCount;
        Transaction _currentTran;
        int _reentrancyFlag;
        bool _deserializing;


        class AllCollection : IReadOnlyCollection<ObservableObject>
        {
            readonly ObservableDomain _d;
            public AllCollection( ObservableDomain d )
            {
                _d = d;
            }

            public int Count => _d._objectCount;

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
                _changeEvents.Clear();
                _newObjects.Clear();
                _propChanged.Clear();
                _firstPropChanged = _lastPropChanged = null;
                return result;
            }


            internal void Rollback( List<ObservableObject> objects, ref int objectCount )
            {
                foreach( var n in _newObjects )
                {
                    objects[n.OId] = null;
                    --objectCount;
                }
                foreach( var e in _changeEvents )
                {
                    if( e is DisposedObjectEvent d )
                    {
                        d.Object.Resurect( d.ObjectId );
                        objects[d.ObjectId] = d.Object;
                        ++objectCount;
                    }
                }
                PropChanged p = _firstPropChanged;
                while( p != null )
                {
                    p.Object.RestoreInitialValue( p.Info.Name, p.InitialValue );
                    p = p.Next;
                }
            }

            public bool IsNewObject( ObservableObject o ) => _newObjects.Contains( o );

            public void OnNewObject( ObservableObject o, int objectId )
            {
                _changeEvents.Add( new NewObjectEvent( o, objectId ) );
                _newObjects.Add( o );
            }

            public void OnDisposeObject( ObservableObject o )
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

            public void OnNewProperty( PropInfo info )
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
            readonly Stack<int> _initialFreeList;

            public Transaction( ObservableDomain d )
            {
                _domain = d;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
                _initialFreeList = new Stack<int>( d._freeList );
            }

            public IReadOnlyList<IObservableEvent> Commit()
            {
                CurrentThreadDomain = _previous;
                _domain._currentTran = null;
                return _domain._changeTracker.Commit();
            }

            public void Dispose()
            {
                if( _domain._currentTran != null )
                {
                    CurrentThreadDomain = null;
                    _domain._deserializing = true;
                    try
                    {
                        _domain._changeTracker.Rollback( _domain._objects, ref _domain._objectCount );
                        _domain._freeList = _initialFreeList;
                        _domain._currentTran = null;
                    }
                    finally
                    {
                        _domain._deserializing = false;
                    }
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

        public ObservableDomain( IActivityMonitor domainMonitor = null )
        {
            DomainNumber = Interlocked.Increment( ref _domainNumber );
            DomainMonitor = domainMonitor ?? new ActivityMonitor( $"Observable Domain n°{DomainNumber}." );
            _objects = new List<ObservableObject>();
            _freeList = new Stack<int>();
            _properties = new Dictionary<string, PropInfo>();
            _changeTracker = new ChangeTracker();
            _reentrancyHandler = new ReetrancyHandler( this );
            _exposedObjects = new AllCollection( this );
        }

        /// <summary>
        /// Gets all the observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: any attempt
        /// to modify one of them will throw.
        /// </summary>
        public IReadOnlyCollection<ObservableObject> AllObjects => _exposedObjects;

        /// <summary>
        /// Unique incrmental number for each domain in the AppDomain.
        /// </summary>
        public int DomainNumber { get; }

        /// <summary>
        /// Gets the monitor that is bound to this domain.
        /// </summary>
        public IActivityMonitor DomainMonitor { get; }

        internal bool IsDeserializing => _deserializing;

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

        public IReadOnlyList<IObservableEvent> Modify( Action actions )
        {
            using( var t = BeginTransaction() )
            {
                actions();
                return t.Commit();
            }
        }

        public void Save( Stream s, bool leaveOpen = false, Encoding encoding = null )
        {
            using( CheckReentrancyOnly() )
            using( var w = new Serializer( s, leaveOpen, encoding ) )
            {
                w.WriteSmallInt32( 0 );
                w.Write( _objectCount );
                w.WriteSmallInt32( _freeList.Count );
                foreach( var i in _freeList ) w.WriteSmallInt32( i );
                w.WriteSmallInt32( _properties.Count );
                foreach( var kv in _properties )
                {
                    w.Write( kv.Key );
                    Debug.Assert( kv.Key == kv.Value.Name );
                    w.WriteSmallInt32( kv.Value.PropertyId );
                }
                Debug.Assert( _objects.Count == _objectCount + _freeList.Count );
                foreach( var o in _objects ) w.WriteObject( o );
            }
        }

        public void Load( Stream s, bool leaveOpen = false, Encoding encoding = null )
        {
            using( DomainMonitor.OpenInfo( $"Loading Domain n°{DomainNumber}." ) )
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
                _objectCount = r.ReadInt32();

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

                count = _objectCount + _freeList.Count;
                ObservableObject[] newObjs = new ObservableObject[count];
                while( --count >= 0 )
                {
                    r.ReadObject<ObservableObject>( x => newObjs[x.OId] = x );
                }
                r.ExecuteDeferredActions();

                _objects.Clear();
                _objects.AddRangeArray( newObjs );
            }
            catch( Exception ex )
            {
                DomainMonitor.Fatal( $"While loading Domain n°{DomainNumber}", ex );
            }
            finally
            {
                _deserializing = false;
            }
        }

        internal int Register( ObservableObject o )
        {
            using( CheckTransactionAndReentrancy() )
            {
                Debug.Assert( o != null && o.Domain == this );
                int idx;
                if( _freeList.Count > 0 )
                {
                    idx = _freeList.Pop();
                    _objects[idx] = o;
                }
                else
                {
                    idx = _objects.Count;
                    _objects.Add( o );
                }
                _changeTracker.OnNewObject( o, idx );
                ++_objectCount;
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
                --_objectCount;
            }
        }

        internal PropertyChangedEventArgs OnPropertyChanged( ObservableObject o, string propertyName, object before, object after )
        {
            if( _deserializing ) return null;
            using( CheckTransactionAndReentrancy() )
            {
                if( !_properties.TryGetValue( propertyName, out var p ) )
                {
                    p = new PropInfo( _properties.Count, propertyName );
                    _changeTracker.OnNewProperty( p );
                    _properties.Add( propertyName, p );
                }
                _changeTracker.OnPropertyChanged( o, p, before, after );
                return p.EventArg;
            }
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
