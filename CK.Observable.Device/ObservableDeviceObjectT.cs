using CK.Core;
using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 1 )]
    public abstract class ObservableDeviceObject<TSidekick, TConfig> : ObservableDeviceObject, ILocalConfiguration<TConfig>, ISidekickClientObject<TSidekick>
        where TSidekick : ObservableDomainSidekick
        where TConfig : DeviceConfiguration, new()
    {
        TConfig _localConfiguration;

        /// <summary>
        /// Initializes a new observable object device.
        /// </summary>
        /// <param name="deviceName">The device name.</param>
        protected ObservableDeviceObject( string deviceName, bool callEnsureSidekick )
            : base( deviceName )
        {

            if( callEnsureSidekick ) Domain.EnsureSidekicks();
            if( _deviceConfiguration != null )
            {
                _localConfiguration = (TConfig)_deviceConfiguration.DeepClone();
            }
            else
            {
                _localConfiguration = new TConfig();
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

        private protected override DeviceConfiguration GetLocalConfiguration() => _localConfiguration;
        private protected override void SetLocalConfiguration( DeviceConfiguration value ) => DoSetLocalConfiguraton( (TConfig)value );

        [NotExportable]
        public new TConfig? DeviceConfiguration => (TConfig?)_deviceConfiguration;

        [NotExportable]
        public new ILocalConfiguration<TConfig> LocalConfiguration => this;

        TConfig ILocalConfiguration<TConfig>.Value
        {
            get => _localConfiguration;
            set => DoSetLocalConfiguraton( value );
        }

        void DoSetLocalConfiguraton( TConfig value )
        {
            Throw.CheckNotNullArgument( value );
            if( value == _deviceConfiguration )
            {
                _localConfiguration = (TConfig)_deviceConfiguration.DeepClone();
            }
            else
            {
                _localConfiguration = value;
            }
        }

        bool ILocalConfiguration<TConfig>.IsDirty => base.LocalConfiguration.IsDirty;

        void ILocalConfiguration<TConfig>.SendDeviceConfigureCommand( DeviceControlAction? deviceControlAction ) => base.LocalConfiguration.SendDeviceConfigureCommand( deviceControlAction );

    }
}
