namespace CK.Observable;

/// <summary>
/// Interface that unifies <see cref="ObservableEvent"/> related to collections.
/// </summary>
public interface ICollectionEvent
{
    /// <summary>
    /// Gets the collection object.
    /// Must be used in read only during the direct handling of the event.
    /// </summary>
    ObservableObject Object { get; }
}
