using System;

namespace CK.Observable.Domain.Tests.Clients
{
    [SerializationVersion( 0 )]
    public class TestObservableRootObject : ObservableRootObject
    {
        public string Prop1 { get; set; }
        public string Prop2 { get; set; }

        public bool TestBehavior__ThrowOnWrite { get; set; }

        public TestObservableRootObject()
        {
        }

        public TestObservableRootObject( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading().Reader;

            Prop1 = r.ReadNullableString();
            Prop2 = r.ReadNullableString();
        }

        void Write( BinarySerializer s )
        {
            s.WriteNullableString( Prop1 );
            s.WriteNullableString( Prop2 );

            if( TestBehavior__ThrowOnWrite ) throw new Exception( $"{nameof( TestBehavior__ThrowOnWrite )} is set. This is a test exception." );
        }
    }
}
