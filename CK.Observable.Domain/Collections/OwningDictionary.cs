using CK.BinarySerialization;
using CK.Core;
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
    [Obsolete( "This must not be used anymore: this doesn't bring much to the table. So this is NOT serializable anymore: OwningDictionary must be reverted to simple ObservableDictionary." )]
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
        protected OwningDictionary( Sliced _ ) : base( _ ) { }

        /// Legacy only and read only, no more Write support.
        OwningDictionary( CK.Observable.IBinaryDeserializer r, TypeReadInfo info )
                : base( Sliced.Instance )
        {
            AutoDestroyValues = r.ReadBoolean();
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
