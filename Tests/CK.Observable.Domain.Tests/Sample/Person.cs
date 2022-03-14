using System.Diagnostics;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion( 1 )]
    public class Person : ObservableObject
    {
        public Person()
        {
        }

        protected Person( RevertSerialization _ ) : base( _ ) { }

        Person( IBinaryDeserializer r, TypeReadInfo? info )
            : base( RevertSerialization.Default )
        {
            Debug.Assert( !IsDestroyed );
            Friend = (Person)r.ReadObject();
            FirstName = r.ReadNullableString();
            LastName = r.ReadNullableString();
            if( info.Version >= 1 )
            {
                Age = r.ReadNonNegativeSmallInt32();
            }
        }

        void Write( BinarySerializer s )
        {
            Debug.Assert( !IsDestroyed );
            s.WriteObject( Friend );
            s.WriteNullableString( FirstName );
            s.WriteNullableString( LastName );
            s.WriteNonNegativeSmallInt32( Age );
        }

        public Person Friend { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        /// <summary>
        /// Gets or sets the age. Defaults to 18.
        /// This property has been added in version 1.
        /// </summary>
        public int Age { get; set; } = 18;

        public override string ToString() => $"'Person {FirstName} {LastName}'";

    }
}
