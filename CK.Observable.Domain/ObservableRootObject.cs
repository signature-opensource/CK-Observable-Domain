using CK.Core;
using System;

namespace CK.Observable
{
    /// <summary>
    /// Defines a root <see cref="ObservableObject"/>.
    /// This object is not disposable and is initialized with its holding domain.
    /// </summary>
    [BinarySerialization.SerializationVersion( 0 )]
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

        #region Old Deserialization
        ObservableRootObject( IBinaryDeserializer r, TypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
        {
        }
        #endregion

        #region New Deserialization
        protected ObservableRootObject( BinarySerialization.Sliced _ ) : base( _ ) { }

        ObservableRootObject( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableRootObject o )
        {
        }
        #endregion
    }
}
