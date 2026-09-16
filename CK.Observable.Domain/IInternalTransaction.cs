using CK.Core;
using System;

namespace CK.Observable;

/// <summary>
/// Abstraction of <see cref="ObservableDomain.Transaction"/> and <see cref="ObservableDomain.InitializationTransaction"/>.
/// </summary>
internal interface IInternalTransaction : IDisposable
{
    DateTime StartTime { get; }

    IActivityMonitor Monitor { get; }

    TransactionResult Commit();

    void AddError( Exception ex );
}
