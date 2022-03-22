using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable
{
    /// <summary>
    /// This must not be used anymore: this doesn't bring much to the table. 
    /// So this is NOT serializable anymore: OwningSet must be reverted to simple ObservableSet.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    [Obsolete( "This must not be used anymore: this doesn't bring much to the table. So this is NOT serializable anymore: OwningSet must be reverted to simple ObservableSet." )]
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
        protected OwningSet( Sliced _ ) : base( _ ) { }

        /// Legacy only and read only, no more Write support.
        OwningSet( CK.Observable.IBinaryDeserializer r, TypeReadInfo info )
                : base( Sliced.Instance )
        {
            AutoDestroyItems = r.ReadBoolean();
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
