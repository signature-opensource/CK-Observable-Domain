using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable
{
    /// <summary>
    /// Specializes a <see cref="ObservableSet{T}"/> (where T must be <see cref="IDestroyableObject"/>)
    /// that destroys its items when destroyed, .
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    [SerializationVersion( 0 )]
    public class OwningSet<T> : ObservableSet<T> where T : IDestroyableObject
    {
        /// <summary>
        /// Initializes a new empty owning set.
        /// </summary>
        public OwningSet()
        {
        }

        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected OwningSet( RevertSerialization _ ) : base( _ ) { }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="info">The type info.</param>
        OwningSet( IBinaryDeserializer r, TypeReadInfo info )
                : base( RevertSerialization.Default )
        {
            AutoDestroyItems = r.ReadBoolean();
        }

        /// <summary>
        /// The serialization method.
        /// </summary>
        /// <param name="w">The target binary serializer.</param>
        void Write( BinarySerializer w )
        {
            w.Write( AutoDestroyItems );
        }

        /// <summary>
        /// Gets or sets whether this owning list should destroy its items.
        /// When set to false it behaves like its base <see cref="ObservableSet{T}"/>.
        /// This obviously defaults to true.
        /// </summary>
        [NotExportable]
        public bool AutoDestroyItems { get; set; } = true;

        protected internal override void OnDestroy()
        {
            if( AutoDestroyItems )
            {
                DestroyItems();
            }
            base.OnDestroy();
        }

    }
}
