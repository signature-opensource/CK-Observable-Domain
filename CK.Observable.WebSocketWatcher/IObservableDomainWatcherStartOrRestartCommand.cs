using CK.Cris;
using CK.TypeScript;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// CRIS command that starts or restarts watching an <see cref="ObservableDomain"/> for a given WebSocket connection.
/// Returns the initial JSON payload: either a full domain export or the missed transaction events since
/// <see cref="TransactionNumber"/>.
/// </summary>
[TypeScriptType]
public interface IObservableDomainWatcherStartOrRestartCommand : ICommand<string>
{
    /// <summary>
    /// The WebSocket connection identifier (provided on connect).
    /// </summary>
    string ConnectionId { get; set; }

    /// <summary>
    /// The name of the <see cref="ObservableDomain"/> to watch.
    /// </summary>
    string DomainName { get; set; }

    /// <summary>
    /// The last known transaction number on the client side.
    /// Use 0 to request a full domain export.
    /// </summary>
    int TransactionNumber { get; set; }
}
