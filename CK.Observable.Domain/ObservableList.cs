using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    [SerializationVersionAttribute(0)]
    public class ObservableList<T> : ObservableObject, IList<T>, IReadOnlyList<T>
    {
        readonly List<T> _list;

        public ObservableList()
        {
            _list = new List<T>();
        }

        protected ObservableList( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            _list = r.ReadObjectList<T>();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObjects( _list.Count, _list );
        }

        void Export( int num, ObjectExporter e )
        {
            //e.Target.EmitStartObject( -1, ObjectExportedKind.Object );
            //e.ExportNamedProperty( ExportContentOIdName, OId );
            //e.Target.EmitPropertyName( ExportContentPropName );
            e.ExportList( num, _list );
            //e.Target.EmitEndObject( -1, ObjectExportedKind.Object );
        }

        public T this[int index]
        {
            get => _list[index];
            set
            {
                _list[index] = value;
                Domain.OnListSetAt( this, index, value );
            }
        }

        public int Count => _list.Count;

        internal override ObjectExportedKind ExportedKind => ObjectExportedKind.List;

        bool ICollection<T>.IsReadOnly => false;

        public void Add( T item )
        {
            Domain.OnListInsert( this, _list.Count, item );
            _list.Add( item );
        }

        public void AddRange( IEnumerable<T> items )
        {
            foreach( var i in items ) Add( i );
        }

        public void Clear()
        {
            Domain.OnCollectionClear( this );
            _list.Clear();
        }

        public void Insert( int index, T item )
        {
            Domain.OnListInsert( this, index, item );
            _list.Insert( index, item );
        }

        public void InsertRange( int index, IEnumerable<T> items )
        {
            foreach( var i in items ) Insert( index++, i );
        }

        public bool Remove( T item )
        {
            int index = _list.IndexOf( item );
            if( index >= 0 )
            {
                RemoveAt( index );
                return true;
            }
            return false;
        }

        public void RemoveAt( int index )
        {
            Domain.OnListRemoveAt( this, index );
            _list.RemoveAt( index );
        }

        public bool Contains( T item ) => _list.Contains( item );
        
        public void CopyTo( T[] array, int arrayIndex ) => _list.CopyTo( array, arrayIndex );

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        public int IndexOf( T item ) => _list.IndexOf( item );

        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
        
    }
}
