using CK.Core;
using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Observable.Device
{
    /// <summary>
    /// Non generic abstract base class for device. It is not intended to be specialized directly: use the
    /// generic <see cref="ObservableDeviceObject{TSidekick}"/> as the object device base.
    /// </summary>
    [SerializationVersion( 1 )]
    public abstract class ObservableDeviceObject : ObservableObject, ISidekickLocator
    {
        DeviceStatus? _status;
        ObservableEventHandler _statusChanged;
        ObservableEventHandler _configurationChanged;
        internal IDeviceBridge _bridge;

#pragma warning disable CS8618 // Non-nullable _bridge uninitialized. Consider declaring as nullable.

        private protected ObservableDeviceObject( string deviceName )
        {
            Throw.CheckNotNullArgument( deviceName );
            DeviceName = deviceName;
        }

        protected ObservableDeviceObject( BinarySerialization.Sliced _ ) : base( _ ) { }

        ObservableDeviceObject( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
        {
            DeviceName = info.Version == 0 ? d.Reader.ReadNullableString()! : d.Reader.ReadString();
            _statusChanged = new ObservableEventHandler( d );
            if( info.Version == 1 )
            {
                _configurationChanged = new ObservableEventHandler( d );
            }
        }

        public static void Write( BinarySerialization.IBinarySerializer d, in ObservableDeviceObject o )
        {
            d.Writer.Write( o.DeviceName );
            o._statusChanged.Write( d );
            o._configurationChanged.Write( d );
        }

#pragma warning restore CS8618

        internal interface IDeviceBridge
        {
            ObservableDomainSidekick Sidekick { get; }

            BaseConfigureDeviceCommand CreateConfigureCommand( DeviceConfiguration? configuration );

            BaseStartDeviceCommand CreateStartCommand();

            BaseStopDeviceCommand CreateStopCommand();

            BaseDestroyDeviceCommand CreateDestroyCommand();

            BaseSetControllerKeyDeviceCommand CreateSetControllerKeyCommand();

            IEnumerable<string> CurrentlyAvailableDeviceNames { get; }

            string? ControllerKey { get; }

            T CreateCommand<T>( Action<T>? configuration ) where T : BaseDeviceCommand, new();
        }

        /// <summary>
        /// Gets the name of this device.
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// Gets the device status.
        /// This is null when no device named <see cref="DeviceName"/> exist in the device host.
        /// </summary>
        public DeviceStatus? Status
        {
            get => _status;
            internal set
            {
                if( _status != value )
                {
                    _status = value;
                    OnPropertyChanged( nameof(Status), value );
                }
            }
        }

        /// <summary>
        /// Raised whenever <see cref="Status"/> has changed.
        /// </summary>
        public event SafeEventHandler StatusChanged
        {
            add => _statusChanged.Add( value, nameof(StatusChanged) );
            remove => _statusChanged.Remove( value );
        }

        /// <summary>
        /// Gets the last successfully applied configuration of this device.
        /// This is null when no device named <see cref="DeviceName"/> exist in the device host.
        /// <para>
        /// A DeviceConfiguration is mutable by design and this is a clone of the last applied configuration:
        /// it can be updated, but this has no effect on the actual device's configuration: to apply
        /// the configuration, <see cref="ApplyDeviceConfiguration"/> must be used.
        /// </para>
        /// </summary>
        [NotExportable]
        public DeviceConfiguration? Configuration { get; private set; }

        /// <summary>
        /// Raised whenever <see cref="Configuration"/> has changed.
        /// </summary>
        public event SafeEventHandler ConfigurationChanged
        {
            add => _configurationChanged.Add( value, nameof( ConfigurationChanged ) );
            remove => _configurationChanged.Remove( value );
        }

        /// <summary>
        /// Called whenever the device's configuration changed.
        /// This base method MUST be called since it sets the <see cref="Configuration"/> property.
        /// </summary>
        /// <param name="configuration">The updated configuration.</param>
        protected internal virtual void OnDeviceConfigurationApplied( DeviceConfiguration? configuration )
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Gets whether the device is under control of this object or the <see cref="IDevice.ControllerKey"/> is null: the device
        /// doesn't restrict the commands.
        /// </summary>
        public bool HasDeviceControl { get; internal set; }

        /// <summary>
        /// Gets whether the device is under control of this object, excluding the other ones.
        /// <para>
        /// A device is under exclusive control of this observable device if and only if its <see cref="IDevice.ControllerKey"/>
        /// is this <see cref="DomainView.DomainName"/>.
        /// </para>
        /// </summary>
        public bool HasExclusiveDeviceControl { get; internal set; }

        /// <summary>
        /// Gets whether this observable object device is bound to a <see cref="IDevice"/>.
        /// Note that <see cref="Status"/> and <see cref="Configuration"/> are both null if this device is unbound and
        /// that this flag is [NotExportable].
        /// </summary>
        [NotExportable]
        public bool IsBoundDevice => Status != null;

        ObservableDomainSidekick ISidekickLocator.Sidekick => _bridge.Sidekick;

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> if this observable object device is not bound
        /// to a <see cref="IDevice"/>.
        /// Note that <see cref="Status"/> and <see cref="Configuration"/> are both null if this device is unbound.
        /// </summary>
        public void ThrowOnUnboundedDevice()
        {
            if( Status == null )
            {
                var msg = $"Device '{DeviceName}' of type '{GetType().Name}' is not bound to any device. Available device names: '{_bridge.CurrentlyAvailableDeviceNames.Concatenate( "', '" )}'.";
                throw new InvalidOperationException( msg );
            }
        }

        /// <summary>
        /// Sends a <see cref="DeviceConfiguration"/> command to the device with the wanted configuration.
        /// <para>
        /// This device may obviously not be bound (<see cref="IsBoundDevice"/> can be false): this configuration
        /// may create the device.
        /// </para>
        /// <para>
        /// To take control of a newly created device, <see cref="DeviceConfiguration.ControllerKey"/> can be
        /// set to this <see cref="DomainView.DomainName"/>.
        /// </para>
        /// </summary>
        public void ApplyDeviceConfiguration( DeviceConfiguration configuration )
        {
            Throw.CheckNotNullArgument( configuration );
            SendBasicCommand( _bridge.CreateConfigureCommand( configuration ) );
        }

        /// <summary>
        /// Sends a start command to the device.
        /// <para>
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </para>
        /// </summary>
        public void SendStartDeviceCommand()
        {
            ThrowOnUnboundedDevice();
            SendBasicCommand( _bridge.CreateStartCommand() );
        }

        /// <summary>
        /// Sends a stop command to the device.
        /// <para>
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </para>
        /// </summary>
        public void SendStopDeviceCommand()
        {
            ThrowOnUnboundedDevice();
            SendBasicCommand( _bridge.CreateStopCommand() );
        }

        /// <summary>
        /// Sends a destroy command to the device.
        /// <para>
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </para>
        /// </summary>
        public void SendDestroyDeviceCommand()
        {
            ThrowOnUnboundedDevice();
            SendBasicCommand( _bridge.CreateDestroyCommand() );
        }

        /// <summary>
        /// Sends a command to take the control of the device if <see cref="HasExclusiveDeviceControl"/> is false.
        /// The <see cref="IDevice.ControllerKey"/> is set to this <see cref="DomainView.DomainName"/>.
        /// <para>
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </para>
        /// </summary>
        public void SendEnsureExclusiveControlCommand()
        {
            ThrowOnUnboundedDevice();
            if( !HasExclusiveDeviceControl )
            {
                var cmd = _bridge.CreateSetControllerKeyCommand();
                cmd.NewControllerKey = Domain.DomainName;
                SendBasicCommand( cmd );
            }
        }

        /// <summary>
        /// Sends a command to release the control of the device if <see cref="HasExclusiveDeviceControl"/> is true.
        /// <para>
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </para>
        /// </summary>
        public void SendReleaseExclusiveControlCommand()
        {
            ThrowOnUnboundedDevice();
            if( HasExclusiveDeviceControl )
            {
                var cmd = _bridge.CreateSetControllerKeyCommand();
                Debug.Assert( cmd.NewControllerKey == null );
                SendBasicCommand( cmd );
            }
        }

        void SendBasicCommand( BaseDeviceCommand cmd )
        {
            cmd.ControllerKey = Domain.DomainName;
            cmd.DeviceName = DeviceName;
            Domain.SendCommand( cmd, _bridge.Sidekick );
        }

        /// <summary>
        /// Creates a new command of the given type with its <see cref="DeviceCommand.DeviceName"/> and <see cref="DeviceCommand.ControllerKey"/>.
        /// Note that if the <see cref="DeviceCommand.HostType"/> is not compatible with the actual <see cref="IDeviceHost"/> of this
        /// <see cref="ObservableDeviceObject{TSidekick}"/> sidekick, a <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <typeparam name="T">The type of the command to create.</typeparam>
        /// <param name="configuration">Optional configurator of the command.</param>
        /// <returns>A ready to use command.</returns>
        protected T CreateDeviceCommand<T>( Action<T>? configuration = null ) where T : BaseDeviceCommand, new() => _bridge.CreateCommand<T>( configuration );

        /// <summary>
        /// Simple helper that calls <see cref="CreateDeviceCommand{T}(Action{T}?)"/> and <see cref="DomainView.SendCommand(in ObservableDomainCommand)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the command to create.</typeparam>
        /// <param name="configuration">Optional configurator of the command.</param>
        protected void SendDeviceCommand<T>( Action<T>? configuration = null ) where T : BaseDeviceCommand, new()
        {
            Domain.SendCommand( CreateDeviceCommand( configuration ), _bridge.Sidekick );
        }

        protected override void OnDestroy()
        {
            _configurationChanged.RemoveAll();
            _statusChanged.RemoveAll();
            // Using nullable just in case EnsureDomainSidekick has not been called.
            ((IInternalObservableDeviceSidekick?)_bridge?.Sidekick)?.OnObjectDestroyed( Domain.Monitor, this );
            base.OnDestroy();
        }
    }
}
