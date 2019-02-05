using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    [SerializationVersion(0)]
    public class ObservableChannel<T> : ObservableObject
    {
        public ObservableChannel()
        {
        }

        protected ObservableChannel( IBinaryDeserializerContext d ) : base( d )
        {
            d.StartReading();
        }

        void Write( BinarySerializer s )
        {
        }

        public void Export( int num, ObjectExporter exporter )
        {
            exporter.ExportList( num, Array.Empty<T>() );
        }

        internal override ObjectExportedKind ExportedKind => ObjectExportedKind.List;

        public void Send( T item )
        {
            Domain.OnListInsert( this, 0, item );
        }

        public void Send( IEnumerable<T> items )
        {
            foreach( var i in items ) Send( i );
        }
        
    }
}
