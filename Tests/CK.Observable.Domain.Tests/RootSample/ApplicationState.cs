using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.RootSample
{
    [SerializationVersion(0)]
    public class ApplicationState : ObservableRootObject
    {
        public ApplicationState( ObservableDomain domain )
            : base( domain )
        {
            ToDoNumbers = new ObservableList<int>();
        }

        protected ApplicationState( BinaryDeserializer d )
            : base( d )
        {
            var r = d.StartReading();
            r.ReadObject<ObservableList<int>>( list => ToDoNumbers = list );
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( ToDoNumbers );
        }

        public ObservableList<int> ToDoNumbers { get; private set; }
    }
}
