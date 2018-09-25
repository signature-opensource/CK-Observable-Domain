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
            Products = new ObservableList<ProductState>();
            // Creating products and disposing them in the ctor create holes
            // in the numbering.
            // This tests this edge case that must be handled properly.
            // Note that the current implicit domain is available.
            var p1 = new ProductState( new Product("unused", 0 ) );
            var p2 = new ProductState( new Product( "unused", 0 ) );
            p1.Dispose();
            p2.Dispose();
        }

        protected ApplicationState( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            ToDoNumbers = (ObservableList<int>)r.ReadObject();
            Products = (ObservableList<ProductState>)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( ToDoNumbers );
            s.WriteObject( Products );
        }

        public ObservableList<int> ToDoNumbers { get; }

        public ObservableList<ProductState> Products { get; }

        public ProductState CurrentProduct { get; set; }

        public void SkipToNextProduct()
        {
            int newIdx = CurrentProduct == null
                            ? 0
                            : (Products.IndexOf( CurrentProduct ) + 1) % Products.Count;
            if( newIdx < Products.Count )
            {
                CurrentProduct = Products[newIdx];
            }
            else CurrentProduct = null;
        }
    }
}
