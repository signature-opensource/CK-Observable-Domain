using CK.Core;

namespace CK.Observable;

/// <summary>
/// Synchronous configurator that is called inside the initial <see cref="ObservableDomain"/> transaction.
/// </summary>
/// <typeparam name="T">The driver type whose domain is being configured.</typeparam>
[IsMultiple]
public interface IObservableDomainConfigurator<T> : IAutoService
    where T : IObservableDomainDriver
{
    /// <summary>
    /// Configures the <paramref name="observableDomain"/> during the driver's initial transaction.
    /// This is typically used to create or initialize observable objects in the domain.
    /// Returning false here is a strong signal that will throw an InvalidOperationException.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="observableDomain">The domain being configured.</param>
    /// <returns>True on success, false on error.</returns>
    bool ConfigureDomain( IActivityMonitor monitor, ObservableDomain observableDomain );
}
