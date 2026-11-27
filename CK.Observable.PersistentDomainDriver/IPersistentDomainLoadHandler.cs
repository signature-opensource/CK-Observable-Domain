using System.Threading.Tasks;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// Receives the load outcome of a <see cref="PersistentDomainDriver{TSelf}"/> at start.
/// All the handlers of a driver type exist before the start, because dependency injection creates them.
/// <para>
/// The driver calls all the handlers after the load and before the initial transaction, as it calls the configurators.
/// The driver logs an exception from a handler and calls the next handler.
/// If a handler returns false or throws, the start fails after the last handler: the driver writes no new generation and renames no file.
/// A handler cannot change the outcome. To get another outcome, change the recovery options.
/// </para>
/// <para>
/// If the recovery chain fails, the driver also calls all the handlers, with a <see cref="PersistentDomainLoadKind.Failed"/> outcome.
/// Then the return value has no effect. The start fails after the last handler.
/// </para>
/// </summary>
/// <typeparam name="T">The driver type.</typeparam>
[IsMultiple]
public interface IPersistentDomainLoadHandler<T> : IAutoService where T : IObservableDomainDriver
{
    /// <summary>
    /// Called once at start, after the load.
    /// When the outcome is <see cref="PersistentDomainLoadKind.Failed"/>, <see cref="IObservableDomainDriver.Domain"/> is null.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="driver">The driver.</param>
    /// <param name="outcome">The load outcome. It is also in <see cref="PersistentDomainDriver{TSelf}.LoadOutcome"/>.</param>
    /// <returns>True to continue the start, false to stop it. Ignored for a Failed outcome.</returns>
    Task<bool> OnDomainLoadedAsync( IActivityMonitor monitor, T driver, PersistentDomainLoadOutcome outcome );
}
