using CK.Core;
using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 1 )]
    public abstract partial class ObservableDeviceObject<TSidekick, TConfig> : ObservableDeviceObject, ISidekickClientObject<TSidekick>
        where TSidekick : ObservableDomainSidekick
        where TConfig : DeviceConfiguration, new()
    {
        TConfig _localConfiguration;

        /// <summary>
        /// Initializes a new observable object device.
        /// </summary>
        /// <param name="deviceName">The device name.</param>
        /// <param name="ensureSidekicks">
        /// Should always be false: <c>Domain.EnsureSidekicks()</c> must be called
        /// by the final, most specialized, class.
        /// </param>
        protected ObservableDeviceObject( string deviceName, bool ensureSidekicks )
            : base( deviceName )
        {

            if( ensureSidekicks ) Domain.EnsureSidekicks();
            if( _deviceConfiguration != null )
            {
                _localConfiguration = (TConfig)_deviceConfiguration.DeepClone();
                _localConfiguration.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
            }
            else
            {
                _localConfiguration = new TConfig();
                _localConfiguration.Name = deviceName;
            }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected ObservableDeviceObject( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        ObservableDeviceObject( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            if( info.Version > 0 )
            {
                _localConfiguration = r.ReadObject<TConfig>();
            }
            else
            {
                _localConfiguration = new TConfig();
            }
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableDeviceObject<TSidekick, TConfig> o )
        {
            s.WriteObject( o._localConfiguration );
        }

        [NotExportable]
        public new TConfig? DeviceConfiguration => (TConfig?)_deviceConfiguration;
    }
}
