using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.Model
{
    [SerializationVersion(0)]
    public class School : ObservableRootObject
    {
        public School()
        {
            Persons = new ObservableList<Person>();
        }

        School( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            Persons = (ObservableList<Person>)r.ReadObject()!;
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Persons );
        }

        public ObservableList<Person> Persons { get; }

    }
}
