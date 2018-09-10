using AutoProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion( 1 )]
    public class Person : ObservableObject
    {
        public Person()
        {
        }

        protected Person( BinaryDeserializer d ) : base( d )
        {
            var r = d.StartReading();
            r.ReadObject<Person>( x => Friend = x );
            FirstName = r.ReadNullableString();
            LastName = r.ReadNullableString();
            if( r.CurrentReadInfo.Version >= 1 )
            {
                Age = r.ReadNonNegativeSmallInt32();
            }
        }

        void Write( BinarySerializer s )
        {
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
