using CK.Core;
using System;

namespace CK.Observable
{
    /// <summary>
    /// Defines a root <see cref="ObservableObject"/>.
    /// This object is not disposable and is initialized with its holding domain.
    /// </summary>
    [SerializationVersion( 0 )]
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
        /// <param name="reusableArgs">
        /// The event arguments that exposes the monitor to use (that is the same as this <see cref="ObservableObject.Monitor"/> protected property).
        /// </param>
        /// <param name="isReloading">Always false: this is never called for root objects.</param>
        protected internal override void OnDisposed( EventMonitoredArgs args, bool isReloading )
        {
            if( isReloading ) base.OnDisposed( args, isReloading );
            else throw new InvalidOperationException( "ObservableRootObject cannot be disposed." );
        }
    }
}
