using CK.Core;
using System.Diagnostics;

namespace CK.Observable.Domain.Tests.Sample;

[SerializationVersion( 1 )]
public class Person : ObservableObject
{
    public Person()
    {
    }

    protected Person( BinarySerialization.Sliced _ ) : base( _ ) { }

    Person( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
    {
        Debug.Assert( !IsDestroyed );
        Friend = r.ReadNullableObject<Person>();
        FirstName = r.Reader.ReadNullableString();
        LastName = r.Reader.ReadNullableString();
        if( info.Version >= 1 )
        {
            Age = r.Reader.ReadNonNegativeSmallInt32();
        }
    }

    public static void Write( BinarySerialization.IBinarySerializer s, in Person o )
    {
        Debug.Assert( !o.IsDestroyed );
        s.WriteNullableObject( o.Friend );
        s.Writer.WriteNullableString( o.FirstName );
        s.Writer.WriteNullableString( o.LastName );
        s.Writer.WriteNonNegativeSmallInt32( o.Age );
    }

    public Person? Friend { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the age. Defaults to 18.
    /// This property has been added in version 1.
    /// </summary>
    public int Age { get; set; } = 18;

    public override string ToString() => $"'Person {FirstName} {LastName}'";

}
