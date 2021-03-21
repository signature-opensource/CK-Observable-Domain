namespace CK.Observable.Domain.Tests
{
    [SerializationVersion( 0 )]
    class InternalSample : InternalObject
    {
        public InternalSample()
        {
        }

        public string? Name { get; set; }

        InternalSample( IBinaryDeserializer r, TypeReadInfo info )
            : base( RevertSerialization.Default )
        {
            Name = r.ReadNullableString();
        }

        void Write( BinarySerializer w )
        {
            w.WriteNullableString( Name );
        }
    }
}
