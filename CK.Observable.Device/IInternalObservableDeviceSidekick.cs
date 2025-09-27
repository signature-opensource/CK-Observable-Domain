using CK.Core;

namespace CK.Observable.Device;

interface IInternalObservableDeviceSidekick : IObservableDeviceSidekick
{
    void OnObjectDestroyed( IActivityMonitor monitor, ObservableDeviceObject o );

    void OnObjectHostDestroyed( IActivityMonitor monitor );
}
