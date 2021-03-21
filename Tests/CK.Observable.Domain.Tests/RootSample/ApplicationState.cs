namespace CK.Observable.Domain.Tests.RootSample
{
    [SerializationVersion(0)]
    public class ApplicationState : ObservableRootObject
    {
        public ApplicationState()
        {
            ToDoNumbers = new ObservableList<int>();
            Products = new ObservableDictionary<string, ProductInfo>();
            ProductStateList = new ObservableList<Product>();
            ProductInfos = new ObservableList<ProductInfo>();
            // Creating products and disposing them in the ctor create holes
            // in the numbering.
            // This tests this edge case that must be handled properly.
            // Note that the current implicit domain is available.
            var p1 = new Product( new ProductInfo("unused", 0 ) );
            var p2 = new Product( new ProductInfo( "unused", 0 ) );
            var p3 = new Product( new ProductInfo( "unused", 0 ) );
            p1.Destroy();
            p2.Destroy();
            p3.Destroy();
        }

        ApplicationState( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            ToDoNumbers = (ObservableList<int>)r.ReadObject();
            Products = (ObservableDictionary<string, ProductInfo>)r.ReadObject();
            ProductStateList = (ObservableList<Product>)r.ReadObject();
            CurrentProductState = (Product)r.ReadObject();
            ProductInfos = (ObservableList<ProductInfo>)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( ToDoNumbers );
            s.WriteObject( Products );
            s.WriteObject( ProductStateList );
            s.WriteObject( CurrentProductState );
            s.WriteObject( ProductInfos );
        }

        public ObservableList<int> ToDoNumbers { get; }

        public ObservableDictionary<string, ProductInfo> Products { get; }

        public ObservableList<ProductInfo> ProductInfos { get; }

        public ObservableList<Product> ProductStateList { get; }

        public Product CurrentProductState { get; set; }

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
