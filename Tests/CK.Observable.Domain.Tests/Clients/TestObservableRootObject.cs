using CK.Core;
using System;

namespace CK.Observable.Domain.Tests.Clients;

[SerializationVersion( 0 )]
public sealed class TestObservableRootObject : ObservableRootObject
{
    public string? Prop1 { get; set; }
    public string? Prop2 { get; set; }

    public bool TestBehavior__ThrowOnWrite { get; set; }

    public TestObservableRootObject()
    {
    }

    TestObservableRootObject( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
    {
        Prop1 = d.Reader.ReadNullableString();
        Prop2 = d.Reader.ReadNullableString();
    }

    public static void Write( BinarySerialization.IBinarySerializer s, in TestObservableRootObject o )
    {
        s.Writer.WriteNullableString( o.Prop1 );
        s.Writer.WriteNullableString( o.Prop2 );

        if( o.TestBehavior__ThrowOnWrite ) throw new Exception( $"{nameof( TestBehavior__ThrowOnWrite )} is set. This is a test exception." );
    }
}
