namespace CK.Observable.Domain.Tests.RootSample
{
    [BinarySerialization.SerializationVersion(0)]
    public class Product : ObservableObject
    {
        public Product( ProductInfo p )
        {
            ProductInfo = p;
        }

        Product( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            ProductNumber = r.Reader.ReadInt32();
            Name = r.Reader.ReadNullableString();
            ProductInfo = r.ReadValue<ProductInfo>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Product o )
        {
            s.Writer.Write( o.ProductNumber );
            s.Writer.WriteNullableString( o.Name );
            s.WriteValue( o.ProductInfo );
        }

        public ProductInfo ProductInfo { get; } 

        public int ProductNumber { get; set; }

        public string Name { get; set; }
    }
}
