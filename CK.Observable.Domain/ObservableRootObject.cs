using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    [SerializationVersion(0)]
    public class ObservableRootObject : ObservableObject
    {
        protected ObservableRootObject( ObservableDomain domain )
            : base( domain )
        {
        }

        protected ObservableRootObject( BinaryDeserializer d )
            : base( d )
        {
            d.StartReading();
        }

        void Write( BinarySerializer w )
        {
        }

        protected override void OnDisposed()
        {
            throw new InvalidOperationException( "ObservableRootObject can not be disposed." );
        }
    }
}
