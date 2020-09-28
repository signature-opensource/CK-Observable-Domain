using CK.DeviceModel;

namespace CK.Observable.Device
{
    [SerializationVersion( 0 )]
    public abstract class ObservableObjectDeviceHost<THost> : ObservableObject
        where THost : IDeviceHost
    {
        internal readonly ObservableList<AvailableDeviceInfo> InternalDevices;

        protected ObservableObjectDeviceHost()
        {
            InternalDevices = new ObservableList<AvailableDeviceInfo>();
            Domain.SidekickActivated += OnSidekickActivated;
        }

        void OnSidekickActivated( object sender, SidekickActivatedEventArgs e )
        {
            if( e.IsOfType<IInternalObservableDeviceSidekick<THost>>() )
            {
                e.RegisterObject( this );
            }
        }

        protected ObservableObjectDeviceHost( IBinaryDeserializerContext ctx )
        {
            ctx.StartReading();
            InternalDevices = new ObservableList<AvailableDeviceInfo>();
        }

        void Write( BinarySerializer s )
        {
        }

        /// <summary>
        /// Gets an observable list of devices that are managed by the device host.
        /// This list is under control of the <see cref="IDeviceHost"/>.
        /// </summary>
        public IObservableReadOnlyList<AvailableDeviceInfo> Devices => InternalDevices;

    }
}
