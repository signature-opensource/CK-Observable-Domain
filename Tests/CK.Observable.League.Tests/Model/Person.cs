using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.Model
{
    [SerializationVersion( 0 )]
    public class Person : ObservableObject
    {
        public Person()
        {
        }

        protected Person( BinarySerialization.Sliced _ ) : base( _ ) { }

        Person( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Friend = r.ReadNullableObject<Person>();
            FirstName = r.Reader.ReadNullableString();
            LastName = r.Reader.ReadNullableString();
            Age = r.Reader.ReadNonNegativeSmallInt32();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Person o )
        {
            s.WriteNullableObject( o.Friend );
            s.Writer.WriteNullableString( o.FirstName );
            s.Writer.WriteNullableString( o.LastName );
            s.Writer.WriteNonNegativeSmallInt32( o.Age );
        }

        public Person? Friend { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public int Age { get; set; } = 18;

        public override string ToString() => $"Person {FirstName} {LastName} ({Age})";

    }
}
