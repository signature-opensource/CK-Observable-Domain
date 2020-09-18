namespace CK.Observable
{
    /// <summary>
    /// <see cref="IObservableDomain"/> with strongly typed <see cref="Root1"/>, <see cref="Root2"/> and <see cref="Root3"/>
    /// observable roots.
    /// </summary>
    /// <typeparam name="T1">Type of the first root object.</typeparam>
    /// <typeparam name="T2">Type of the second root object.</typeparam>
    /// <typeparam name="T3">Type of the third root object.</typeparam>
    public interface IObservableDomain<out T1, out T2, out T3> : IObservableDomain
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
        where T3 : ObservableRootObject
    {
        /// <summary>
        /// Gets the first typed root object.
        /// </summary>
        T1 Root1 { get; }

        /// <summary>
        /// Gets the second typed root object.
        /// </summary>
        T2 Root2 { get; }

        /// <summary>
        /// Gets the third typed root object.
        /// </summary>
        T3 Root3 { get; }

    }
}
