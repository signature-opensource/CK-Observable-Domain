using CK.DeviceModel;

namespace CK.Observable.Device.Tests;

public class SampleCommand : DeviceCommand<SampleDeviceHost>
{
    public string? MessagePrefix { get; set; } 
}
