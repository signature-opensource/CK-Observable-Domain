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

        School( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            var r = ctx.StartReading();
            Persons = (ObservableList<Person>)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Persons );
        }

        public ObservableList<Person> Persons { get; }

    }
}
