using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.RootSample
{
    [SerializationVersion(0)]
    public class ProductState : ObservableObject
    {
        public ProductState( Product p )
        {
            Product = p;
        }

        public ProductState( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            ProductNumber = r.ReadInt32();
            Name = r.ReadNullableString();
            Product = (Product)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.Write( ProductNumber );
            s.WriteNullableString( Name );
            s.WriteObject( Product );
        }

        public Product Product { get; } 

        public int ProductNumber { get; set; }

        public string Name { get; set; }
    }
}
