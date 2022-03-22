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

        protected Person( RevertSerialization _ ) : base( _ ) { }

        Person( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            Friend = (Person?)r.ReadObject();
            FirstName = r.ReadNullableString();
            LastName = r.ReadNullableString();
            Age = r.ReadNonNegativeSmallInt32();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( Friend );
            s.WriteNullableString( FirstName );
            s.WriteNullableString( LastName );
            s.WriteNonNegativeSmallInt32( Age );
        }

        public Person? Friend { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public int Age { get; set; } = 18;

        public override string ToString() => $"Person {FirstName} {LastName} ({Age})";

    }
}
