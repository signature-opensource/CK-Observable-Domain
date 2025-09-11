namespace CK.Observable;

/// <summary>
/// This interface generalizes <see cref="InternalObject"/> and <see cref="ObservableObject"/> and ony them (as it can
/// only be implemented in this assembly).
/// </summary>
public interface IObservableDomainObject : IDestroyableObject
{
    new internal void LocalImplementationOnly();
}
