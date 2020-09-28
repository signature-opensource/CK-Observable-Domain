using CK.DeviceModel;

namespace CK.Observable.Device
{

    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableObjectDevice<THost> : ObservableObject
        where THost : IDeviceHost
    {
        protected ObservableObjectDevice( string deviceName )
        {
            DeviceName = deviceName;
            Domain.SidekickActivated += OnSidekickActivated;
        }

        void OnSidekickActivated( object sender, SidekickActivatedEventArgs e )
        {
            if( e.IsOfType<IInternalObservableDeviceSidekick<THost>>() )
            {
                e.RegisterObject( this );
            }
        }

        protected ObservableObjectDevice( IBinaryDeserializerContext ctx )
        {
            var r = ctx.StartReading().Reader;
            DeviceName = r.ReadNullableString();
        }

        void Write( BinarySerializer s )
        {
            s.WriteNullableString( DeviceName );
        }

        /// <summary>
        /// Gets the name of this device.
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// Gets the device status.
        /// This is null when no device named <see cref="DeviceName"/> exist in the device host.
        /// </summary>
        public DeviceStatus? Status { get; internal set; }

        /// <summary>
        /// Gets the current configuration status of this device.
        /// This is null when no device named <see cref="DeviceName"/> exist in the device host.
        /// </summary>
        public DeviceConfigurationStatus? ConfigurationStatus { get; internal set; }

        /// <summary>
        /// Gets whether the device is under control of this object.
        /// </summary>
        public bool HasDeviceControl { get; internal set; }

        /// <summary>
        /// Gets the request .
        /// </summary>
        public DeviceControlRequestStatus ControlRequestStatus { get; internal set; }

        /// <summary>
        /// Attempts to obtain the control of the device.
        /// </summary>
        public void RequestDeviceControl()
        {
            if( !HasDeviceControl && ControlRequestStatus != DeviceControlRequestStatus.RequestingControl )
            {
                ControlRequestStatus = DeviceControlRequestStatus.RequestingControl;
                Domain.SendCommand()
            }
        }

    }
}
