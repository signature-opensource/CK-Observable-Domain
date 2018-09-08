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
        public Product()
        {
        }

        public Product( BinaryDeserializer d )
            : base( d )
        {
            var r = d.StartReading();
            ProductNumber = r.ReadInt32();
            Name = r.ReadNullableString();
        }

        void Write( BinarySerializer s )
        {
            s.Write( ProductNumber );
            s.WriteNullableString( Name );
        }

        public int ProductNumber { get; set; }

        public string Name { get; set; }
    }
}
