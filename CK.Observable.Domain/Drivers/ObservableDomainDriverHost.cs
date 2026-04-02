using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// Central registry of all <see cref="IObservableDomainDriver"/> in the application.
/// <para>
/// On initialization, discovers every <see cref="IObservableDomainDriver"/> from the StObj map
/// and indexes them by <see cref="IObservableDomainDriver.DomainName"/>.
/// On host start, invokes all registered <see cref="IObservableDomainAsyncConfigurator"/>.
/// </para>
/// </summary>
public sealed class ObservableDomainDriverHost : IRealObject
{
    [AllowNull]
    private Dictionary<string, IObservableDomainDriver> _drivers;

    /// <summary>
    /// Gets all registered drivers keyed by their <see cref="IObservableDomainDriver.DomainName"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IObservableDomainDriver> Drivers => _drivers;

    private void StObjInitialize( IActivityMonitor monitor, IStObjObjectMap stObjMap )
    {
        _drivers = stObjMap.FinalImplementations
            .Select( impl => impl.Implementation )
            .OfType<IObservableDomainDriver>()
            .ToDictionary( driver => driver.DomainName );
    }

    private async Task OnHostStartAsync( IActivityMonitor monitor, IEnumerable<IObservableDomainAsyncConfigurator> configurators )
    {
        foreach( var configurator in configurators )
        {
            await configurator.ConfigureHostAsync( monitor, this );
        }
    }
}
