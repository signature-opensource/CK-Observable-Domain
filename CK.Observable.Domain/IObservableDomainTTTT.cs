namespace CK.Observable;

/// <summary>
/// <see cref="IObservableDomain"/> with strongly typed <see cref="Root1"/>, <see cref="Root2"/>,
/// <see cref="Root3"/> and <see cref="Root4"/> observable roots.
/// </summary>
/// <typeparam name="T1">Type of the first root object.</typeparam>
/// <typeparam name="T2">Type of the second root object.</typeparam>
/// <typeparam name="T3">Type of the third root object.</typeparam>
/// <typeparam name="T4">Type of the fourth root object.</typeparam>
public interface IObservableDomain<out T1, out T2, out T3, out T4> : IObservableDomain
    where T1 : ObservableRootObject
    where T2 : ObservableRootObject
    where T3 : ObservableRootObject
    where T4 : ObservableRootObject
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

    /// <summary>
    /// Gets the fourth typed root object.
    /// </summary>
    T4 Root4 { get; }

}
