using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League;

/// <summary>
/// Provides a tracking reference and a isolation shell on a loaded typed <see cref="IObservableDomain{T}"/>
/// in a <see cref="ObservableLeague"/>.
/// <para>
/// The <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once this domain is no more required.
/// </para>
/// <para>
/// Note that this <see cref="IAsyncDisposable.DisposeAsync"/> implementation will use the activity monitor that has been
/// used to <see cref="IObservableDomainLoader.LoadAsync(IActivityMonitor)"/> this shell.
/// </para>
/// </summary>
/// <typeparam name="T">The observable root type.</typeparam>
public interface IObservableDomainShell<out T> : IObservableDomainAccess<T>, IObservableDomainShell
    where T : ObservableRootObject
{
}
