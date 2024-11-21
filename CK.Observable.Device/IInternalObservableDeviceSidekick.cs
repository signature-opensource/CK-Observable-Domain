using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device;

interface IInternalObservableDeviceSidekick : IObservableDeviceSidekick
{
    void OnObjectDestroyed( IActivityMonitor monitor, ObservableDeviceObject o );

    void OnObjectHostDestroyed( IActivityMonitor monitor );
}
