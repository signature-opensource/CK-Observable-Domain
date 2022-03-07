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
    /// So this is NOT serializable anymore: OwningList must be reverted to simple ObservableList.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    [Obsolete("This must not be used anymore: this doesn't bring much to the table. So this is NOT serializable anymore: OwningList must be reverted to simple ObservableList.")]
    [SerializationVersion(0)]
    public class OwningList<T> : ObservableList<T> where T : IDestroyableObject
    {
        /// <summary>
        /// Initializes a new list that will destroy its items when it is itself destroyed.
        /// </summary>
        public OwningList()
        {
        }

        protected OwningList( Sliced _ ) : base( _ ) { }

        /// Legacy only and read only, no more Write support.
        OwningList( CK.Observable.IBinaryDeserializer r, TypeReadInfo info )
            : base( Sliced.Instance )
        {
            AutoDestroyItems = r.ReadBoolean();
        }

        /// <summary>
        /// Gets or sets whether this owning list should destroy its items.
        /// When set to false it behaves like its base <see cref="ObservableList{T}"/>.
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
