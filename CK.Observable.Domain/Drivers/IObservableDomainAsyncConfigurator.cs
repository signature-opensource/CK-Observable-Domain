using System.Threading.Tasks;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// Async configurator that is called once on host startup to configure
/// the <see cref="ObservableDomainDriverHost"/> and its registered drivers.
/// <para>
/// Multiple implementations can coexist: all of them are called during the host start phase.
/// </para>
/// </summary>
[IsMultiple]
public interface IObservableDomainAsyncConfigurator : IAutoService
{
    /// <summary>
    /// Configures the <paramref name="host"/> during its startup phase.
    /// Returning false here is a strong signal that will throw an InvalidOperationException.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="host">The driver host being started.</param>
    /// <returns>True on success, false on error.</returns>
    Task<bool> ConfigureHostAsync( IActivityMonitor monitor, ObservableDomainDriverHost host );
}
