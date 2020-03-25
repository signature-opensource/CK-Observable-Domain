namespace CK.Observable
{
    /// <summary>
    /// <see cref="IObservableDomain"/> with a strongly typed <see cref="Root"/>.
    /// </summary>
    /// <typeparam name="T">Type of the root object.</typeparam>
    public interface IObservableDomain<out T> : IObservableDomain where T : ObservableRootObject
    {
        /// <summary>
        /// Gets the typed root object.
        /// </summary>
        T Root { get; }

    }
}
