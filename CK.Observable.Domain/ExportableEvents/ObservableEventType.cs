namespace CK.Observable
{
    /// <summary>
    /// Models the 10 fundamental observable events required to synchronize any remote domain.
    /// </summary>
    public enum ObservableEventType
    {
        /// <summary>
        /// Non applicable.
        /// </summary>
        None,

        /// <summary>
        /// Event for new <see cref="ObservableObject"/>. See <see cref="NewObjectEvent"/>.
        /// </summary>
        NewObject,

        /// <summary>
        /// Event for disposed <see cref="ObservableObject"/>. See <see cref="DisposedObjectEvent"/>.
        /// </summary>
        DisposedObject,

        /// <summary>
        /// Event for new property. See <see cref="NewPropertyEvent"/>.
        /// </summary>
        NewProperty,

        /// <summary>
        /// Event for property changed. See <see cref="PropertyChangedEvent"/>.
        /// </summary>
        PropertyChanged,

        /// <summary>
        /// Event for insertion in a list. See <see cref="ListInsertEvent"/>.
        /// </summary>
        ListInsert,

        /// <summary>
        /// Event for a clear of any collection. See <see cref="CollectionClearEvent"/>.
        /// </summary>
        CollectionClear,

        /// <summary>
        /// Event for item removed from a list. See <see cref="ListRemoveAtEvent"/>.
        /// </summary>
        ListRemoveAt,

        /// <summary>
        /// Event for indexed item set in a list. See <see cref="ListSetAtEvent"/>.
        /// </summary>
        ListSetAt,

        /// <summary>
        /// Event for a key removal. <see cref="CollectionRemoveKeyEvent"/>.
        /// </summary>
        CollectionRemoveKey,

        /// <summary>
        /// Event for a new association key/value in a map. <see cref="CollectionMapSetEvent"/>.
        /// </summary>
        CollectionMapSet
    }
}
