using CK.Core;

namespace CK.Observable;

/// <summary>
/// Defines a driver that owns and manages an <see cref="ObservableDomain"/>.
/// <para>
/// Each driver is identified by its <see cref="DomainName"/> and is registered
/// in the <see cref="ObservableDomainDriverHost"/> at startup.
/// </para>
/// </summary>
[IsMultiple]
public interface IObservableDomainDriver
{
    /// <summary>
    /// Gets the unique name of the <see cref="ObservableDomain"/> managed by this driver.
    /// </summary>
    string DomainName { get; }

    /// <summary>
    /// Gets the <see cref="ObservableDomain"/> managed by this driver.
    /// </summary>
    ObservableDomain Domain { get; }

    /// <summary>
    /// Gets the <see cref="JsonEventCollector"/> that captures transaction events from the <see cref="Domain"/>.
    /// </summary>
    JsonEventCollector EventCollector { get; }
}
