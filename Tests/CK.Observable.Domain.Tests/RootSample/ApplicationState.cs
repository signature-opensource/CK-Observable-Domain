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
        protected ApplicationState( ObservableDomain domain )
            : base( domain )
        {
            ToDoNumbers = new ObservableList<int>();
            Products = new ObservableList<Product>();
            // Creating products and disposing them in the ctor create holes
            // in the numbering.
            // This tests this edge case that must be handled properly.
            // Note that the current implicit domain is available.
            var p1 = new Product();
            var p2 = new Product();
            p1.Dispose();
            p2.Dispose();
        }

        protected ApplicationState( BinaryDeserializer d )
            : base( d )
        {
            var r = d.StartReading();
            r.ReadObject<ObservableList<int>>( list => ToDoNumbers = list );
            r.ReadObject<ObservableList<Product>>( p => Products = p );
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( ToDoNumbers );
            s.WriteObject( Products );
        }

        public ObservableList<int> ToDoNumbers { get; private set; }

        public ObservableList<Product> Products { get; private set; }
    }
}
