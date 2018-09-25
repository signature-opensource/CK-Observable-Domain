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
            Products = new ObservableDictionary<string, Product>();
            ProductStateList = new ObservableList<ProductState>();
            // Creating products and disposing them in the ctor create holes
            // in the numbering.
            // This tests this edge case that must be handled properly.
            // Note that the current implicit domain is available.
            var p1 = new ProductState( new Product("unused", 0 ) );
            var p2 = new ProductState( new Product( "unused", 0 ) );
            var p3 = new ProductState( new Product( "unused", 0 ) );
            p1.Dispose();
            p2.Dispose();
            p3.Dispose();
        }

        protected ApplicationState( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            ToDoNumbers = (ObservableList<int>)r.ReadObject();
            Products = (ObservableDictionary<string, Product>)r.ReadObject();
            ProductStateList = (ObservableList<ProductState>)r.ReadObject();
            CurrentProductState = (ProductState)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( ToDoNumbers );
            s.WriteObject( Products );
            s.WriteObject( ProductStateList );
            s.WriteObject( CurrentProductState );
        }

        public ObservableList<int> ToDoNumbers { get; }

        public ObservableDictionary<string,Product> Products { get; }

        public ObservableList<ProductState> ProductStateList { get; }

        public ProductState CurrentProductState { get; set; }

        public void SkipToNextProduct()
        {
            int newIdx = CurrentProductState == null
                            ? 0
                            : (ProductStateList.IndexOf( CurrentProductState ) + 1) % ProductStateList.Count;
            if( newIdx < ProductStateList.Count )
            {
                CurrentProductState = ProductStateList[newIdx];
            }
            else CurrentProductState = null;
        }
    }
}
