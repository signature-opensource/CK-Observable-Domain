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
        /// Initializes a new root for the current domain that is retrieved automatically: it
        /// is the last one on the current thread that has started a transaction (see <see cref="ObservableDomain.BeginTransaction"/>).
        /// </summary>
        protected ObservableRootObject()
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
        /// <param name="shouldCleanup">Always false: this method is never called for root objects except when reloading.</param>
        protected internal override void Dispose( bool shouldCleanup )
        {
            if( !shouldCleanup ) base.Dispose( shouldCleanup );
            else throw new InvalidOperationException( "ObservableRootObject cannot be disposed." );
        }
    }
}
