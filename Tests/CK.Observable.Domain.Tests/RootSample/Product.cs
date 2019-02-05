using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.RootSample
{
    [SerializationVersion(0)]
    public class Product : ObservableObject
    {
        public Product( ProductInfo p )
        {
            ProductInfo = p;
        }

        public Product( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            ProductNumber = r.ReadInt32();
            Name = r.ReadNullableString();
            ProductInfo = (ProductInfo)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.Write( ProductNumber );
            s.WriteNullableString( Name );
            s.WriteObject( ProductInfo );
        }

        public ProductInfo ProductInfo { get; } 

        public int ProductNumber { get; set; }

        public string Name { get; set; }
    }
}
