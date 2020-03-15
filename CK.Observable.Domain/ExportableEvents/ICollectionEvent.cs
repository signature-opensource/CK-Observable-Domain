namespace CK.Observable
{
    /// <summary>
    /// Internal interface that unifies events related to collection.
    /// </summary>
    interface ICollectionEvent
    {
        ObservableObject Object { get; }
    }
}
