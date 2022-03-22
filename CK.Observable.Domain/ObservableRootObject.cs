using CK.BinarySerialization;
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
        /// Gets or sets a flag that allows <see cref="ObservableObject.Destroy()"/> to be called on
        /// root objects that are not real roots (they don't belong to <see cref="ObservableDomain.AllRoots"/>).
        /// <para>
        /// Defaults to false: this should not be needed (but this can be useful to cleanup a domain).
        /// </para>
        /// </summary>
        public static bool AllowRootObjectDestroying { get; set; } = false;

        /// <summary>
        /// Initializes a new root for the current domain that is retrieved automatically: it
        /// is the last one on the current thread that has started a transaction (see <see cref="ObservableDomain.BeginTransaction"/>).
        /// </summary>
        protected ObservableRootObject()
        {
        }

        protected ObservableRootObject( Sliced _ ) : base( _ ) { }

        ObservableRootObject( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
        {
        }

        public static void Write( IBinarySerializer s, in ObservableRootObject o )
        {
        }
    }
}
