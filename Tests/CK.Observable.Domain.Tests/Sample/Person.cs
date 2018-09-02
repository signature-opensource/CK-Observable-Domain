using AutoProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersionAttribute( 0 )]
    public class Person : ObservableObject
    {
        public Person()
        {
        }

        protected Person( Deserializer d ) : base( d )
        {
            var r = d.StartReading();
            r.ReadObject<Person>( x => Friend = x );
            FirstName = r.ReadNullableString();
            LastName = r.ReadNullableString();
        }

        void Write( Serializer s )
        {
            s.WriteObject( Friend );
            s.WriteNullableString( FirstName );
            s.WriteNullableString( LastName );
        }

        public Person Friend { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public override string ToString() => $"'Person {FirstName} {LastName}'";

    }
}
