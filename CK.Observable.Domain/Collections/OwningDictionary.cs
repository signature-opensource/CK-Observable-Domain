using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable
{
    /// <summary>
    /// Specializes a <see cref="ObservableDictionary{TKey, TValue}"/> (where TValue must be <see cref="IDestroyableObject"/>)
    /// that destroys its values when destroyed, .
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    [SerializationVersion( 0 )]
    public class OwningDictionary<TKey,TValue> : ObservableDictionary<TKey,TValue>
        where TKey : notnull
        where TValue : IDestroyableObject 
    {
        /// <summary>
        /// Initializes a new empty owning dictionary.
        /// </summary>
        public OwningDictionary()
        {
        }

        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected OwningDictionary( RevertSerialization _ ) : base( _ ) { }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="info">The type info.</param>
        OwningDictionary( IBinaryDeserializer r, TypeReadInfo info )
                : base( RevertSerialization.Default )
        {
            AutoDestroyValues = r.ReadBoolean();
        }

        /// <summary>
        /// The serialization method.
        /// </summary>
        /// <param name="w">The target binary serializer.</param>
        void Write( BinarySerializer w )
        {
            w.Write( AutoDestroyValues );
        }

        /// <summary>
        /// Gets or sets whether this owning dictionary should destroy its items.
        /// When set to false it behaves like its base <see cref="ObservableDictionary{TKey, TValue}"/>.
        /// This obviously defaults to true.
        /// </summary>
        [NotExportable]
        public bool AutoDestroyValues { get; set; } = true;

        protected internal override void OnDestroy()
        {
            if( AutoDestroyValues )
            {
                DestroyValues();
            }
            base.OnDestroy();
        }

    }
}
