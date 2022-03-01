namespace CK.Observable.Domain.Tests
{
    [BinarySerialization.SerializationVersion( 0 )]
    class InternalSample : InternalObject
    {
        public InternalSample()
        {
        }

        public string? Name { get; set; }

        InternalSample( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            Name = r.Reader.ReadNullableString();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in InternalSample o )
        {
            w.Writer.WriteNullableString( o.Name );
        }
    }
}
