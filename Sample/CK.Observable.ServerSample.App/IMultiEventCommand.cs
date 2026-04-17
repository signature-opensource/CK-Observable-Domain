using CK.Cris;

namespace CK.Observable.ServerSample.App;

/// <summary>
/// Sets multiple properties in a single transaction to produce multiple domain events.
/// Used to test the WebSocket watcher's handling of multi-event transactions.
/// </summary>
public interface IMultiEventCommand : ICommand
{
    float SliderValue { get; set; }
    string Label { get; set; }
    int Counter { get; set; }
}
