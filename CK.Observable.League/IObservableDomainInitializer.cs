using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League;


/// <summary>
/// Optional long-lived object that a league created by <see cref="ObservableLeague.LoadAsync(IActivityMonitor, IStreamStore, IObservableDomainInitializer, IServiceProvider?)"/>
/// will use to initialize brand new <see cref="ObservableDomain"/>.
/// <para>
/// The <see cref="IDefaultObservableDomainInitializer"/> is the corresponding optional singleton auto service that can initialize the <see cref="DefaultObservableLeague"/>
/// singleton.
/// </para>
/// </summary>
public interface IObservableDomainInitializer
{
    /// <summary>
    /// Does whatever is needed to initialize the newly created domain.
    /// The only piece of information available here to bind to/recover/lookup data required
    /// to populate the domain is the <see cref="ObservableDomain.DomainName"/> and this is intended:
    /// names matter.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="d">The domain to initialize.</param>
    Task InitializeAsync( IActivityMonitor monitor, ObservableDomain d );
}
