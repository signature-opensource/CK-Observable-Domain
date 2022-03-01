using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League.Tests.Model
{
    [BinarySerialization.SerializationVersion(0)]
    public class School : ObservableRootObject
    {
        public School()
        {
            Persons = new ObservableList<Person>();
        }

        School( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Persons = r.ReadObject<ObservableList<Person>>();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in School o )
        {
            w.WriteObject( o.Persons );
        }

        public ObservableList<Person> Persons { get; }

    }
}
