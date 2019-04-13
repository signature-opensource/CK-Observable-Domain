using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Defines a root <see cref="ObservableObject"/>.
    /// This object is not disposable and is initialized with its holding domain.
    /// </summary>
    [SerializationVersion(0)]
    public class ObservableRootObject : ObservableObject
    {
        /// <summary>
        /// Initializes a new root of a domain.
        /// </summary>
        /// <param name="domain">The holding domain.</param>
        protected ObservableRootObject( ObservableDomain domain )
            : base( domain )
        {
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="d">The deserialization context.</param>
        protected ObservableRootObject( IBinaryDeserializerContext d )
            : base( d )
        {
            d.StartReading();
        }

        void Write( BinarySerializer w )
        {
        }

        /// <summary>
        /// Overridden to throw <see cref="InvalidOperationException"/>.
        /// </summary>
        protected override void OnDisposed()
        {
            throw new InvalidOperationException( "ObservableRootObject can not be disposed." );
        }
    }
}
