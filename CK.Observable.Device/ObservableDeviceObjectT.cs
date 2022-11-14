using CK.Core;
using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 1 )]
    public abstract class ObservableDeviceObject<TSidekick,TConfig> : ObservableDeviceObject, ISidekickClientObject<TSidekick>
        where TSidekick : ObservableDomainSidekick
        where TConfig : DeviceConfiguration,new()
    {
        readonly DeviceConfigurationEditor<TConfig> _deviceConfigurationEditor;

        /// <summary>
        /// Initializes a new observable object device.
        /// </summary>
        /// <param name="deviceName">The device name.</param>
        protected ObservableDeviceObject( string deviceName )
            : base( deviceName )
        {
            _deviceConfigurationEditor = new DeviceConfigurationEditor<TConfig>(this);
        }

        protected ObservableDeviceObject( BinarySerialization.Sliced _ ) : base( _ ) { }

        ObservableDeviceObject( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            if(info.Version > 0 )
            {
                _deviceConfigurationEditor = r.ReadObject<DeviceConfigurationEditor<TConfig>>();
            }
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableDeviceObject<TSidekick,TConfig> o )
        {
            s.WriteObject( o._deviceConfigurationEditor );
        }

        [NotExportable]
        public new TConfig? DeviceConfiguration => (TConfig?)base.DeviceConfiguration;

        [NotExportable]
        public DeviceConfigurationEditor<TConfig> DeviceConfigurationEditor => _deviceConfigurationEditor;

    }
}
