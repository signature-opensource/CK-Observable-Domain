using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League;

/// <summary>
/// Optional interface to be used by <see cref="DefaultObservableLeague"/>.
/// </summary>
public interface IDefaultObservableDomainInitializer : IObservableDomainInitializer, ISingletonAutoService
{
}
