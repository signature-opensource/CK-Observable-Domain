using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Observable.Device
{
    /// <summary>
    /// Non generic abstract base class for device. It is not intended to be specialized directly: use the
    /// generic <see cref="ObservableDeviceObject{TSidekick}"/> as the object device base.
    /// </summary>
    [SerializationVersion( 2 )]
    public abstract partial class ObservableDeviceObject : ObservableObject, ISidekickLocator
    {
        ObservableEventHandler _isRunningChanged;
        ObservableEventHandler _configurationChanged;
        ObservableEventHandler _deviceControlStatusChanged;
        internal IInternalDeviceBridge _bridge;
        DeviceConfiguration? _configuration;
        DeviceControlStatus _deviceControlStatus;
        bool? _isRunning;
        bool _wantPersistentOwnership;

#pragma warning disable CS8618 // Non-nullable _bridge uninitialized. Consider declaring as nullable.

        private protected ObservableDeviceObject( string deviceName )
        {
            Throw.CheckNotNullArgument( deviceName );
            DeviceName = deviceName;
        }

        protected ObservableDeviceObject( Sliced _ ) : base( _ ) { }

        ObservableDeviceObject( IBinaryDeserializer d, ITypeReadInfo info )
            : base( Sliced.Instance )
        {
            DeviceName = info.Version == 0 ? d.Reader.ReadNullableString()! : d.Reader.ReadString();
            _isRunningChanged = new ObservableEventHandler( d );
            if( info.Version > 0 )
            {
                _configurationChanged = new ObservableEventHandler( d );
            }
            if( info.Version > 1 )
            {
                _deviceControlStatusChanged = new ObservableEventHandler( d );
                _isRunning = d.Reader.ReadNullableBool();
                _deviceControlStatus = (DeviceControlStatus)d.Reader.ReadByte();
                _wantPersistentOwnership = d.Reader.ReadBoolean();
            }
        }

        public static void Write( IBinarySerializer d, in ObservableDeviceObject o )
        {
            d.Writer.Write( o.DeviceName );
            o._isRunningChanged.Write( d );
            o._configurationChanged.Write( d );
            o._deviceControlStatusChanged.Write( d );
            d.Writer.WriteNullableBool( o._isRunning );
            d.Writer.Write( (byte)o._deviceControlStatus );
            d.Writer.Write( o._wantPersistentOwnership );
        }

#pragma warning restore CS8618

        internal interface IInternalDeviceBridge : ISidekickLocator
        {
            BaseConfigureDeviceCommand CreateConfigureCommand( DeviceConfiguration? configuration );

            BaseStartDeviceCommand CreateStartCommand();

            BaseStopDeviceCommand CreateStopCommand();

            BaseDestroyDeviceCommand CreateDestroyCommand();

            BaseSetControllerKeyDeviceCommand CreateSetControllerKeyCommand();

            IEnumerable<string> CurrentlyAvailableDeviceNames { get; }

            //string? ControllerKey { get; }

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
        public bool? IsRunning => _isRunning;

        internal bool SetIsRunning( bool? r )
        {
            if( _isRunning != r )
            {
                _isRunning = r;
                OnIsRunningChanged();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called when <see cref="IsRunning"/> changed.
        /// <para>
        /// When overridden, this base method MUST be called to raise <see cref="IsRunningChanged"/> event.
        /// </para>
        /// </summary>
        protected virtual void OnIsRunningChanged()
        {
            OnPropertyChanged( nameof( IsRunning ), _isRunning );
            if( _isRunningChanged.HasHandlers ) _isRunningChanged.Raise( this );
        }

        /// <summary>
        /// Raised whenever <see cref="IsRunning"/> has changed.
        /// </summary>
        public event SafeEventHandler IsRunningChanged
        {
            add => _isRunningChanged.Add( value );
            remove => _isRunningChanged.Remove( value );
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
        public DeviceConfiguration? Configuration => _configuration;

        /// <summary>
        /// Called whenever the device's configuration changed.
        /// </summary>
        /// <param name="configuration">The updated configuration.</param>
        internal bool OnDeviceConfigurationApplied( DeviceConfiguration? configuration )
        {
            var previous = _configuration;
            if( previous != configuration )
            {
                _configuration = configuration;
                OnConfigurationChanged( previous );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called when <see cref="Configuration"/> changed.
        /// <para>
        /// When overridden, this base method MUST be called to raise <see cref="ConfigurationChanged"/> event.
        /// </para>
        /// </summary>
        protected virtual void OnConfigurationChanged( DeviceConfiguration? previousConfiguration )
        {
            OnPropertyChanged( nameof( Configuration ), _configuration );
            if( _configurationChanged.HasHandlers ) _configurationChanged.Raise( this );
        }

        /// <summary>
        /// Raised whenever <see cref="Configuration"/> has changed.
        /// </summary>
        public event SafeEventHandler ConfigurationChanged
        {
            add => _configurationChanged.Add( value );
            remove => _configurationChanged.Remove( value );
        }

        /// <summary>
        /// Gets this device control status.
        /// </summary>
        public DeviceControlStatus DeviceControlStatus => _deviceControlStatus;

        internal bool SetDeviceControlStatus( DeviceControlStatus s )
        {
            if( _deviceControlStatus != s )
            {
                _deviceControlStatus = s;
                OnDeviceControlStatusChanged();
                return true;
            }
            return false;
        }

        protected virtual void OnDeviceControlStatusChanged()
        {
            OnPropertyChanged( nameof( DeviceControlStatus ), _deviceControlStatus );
            if( _deviceControlStatusChanged.HasHandlers ) _deviceControlStatusChanged.Raise( this );
        }

        /// <summary>
        /// Raised whenever <see cref="DeviceControlStatus"/> has changed.
        /// </summary>
        public event SafeEventHandler DeviceControlStatusChanged
        {
            add => _deviceControlStatusChanged.Add( value );
            remove => _deviceControlStatusChanged.Remove( value );
        }

        /// <summary>
        /// Gets whether this observable object device is bound to a <see cref="IDevice"/>.
        /// Note that <see cref="IsRunning"/> and <see cref="Configuration"/> are both null if this device is unbound.
        /// This flag is [NotExportable].
        /// </summary>
        [NotExportable]
        public bool IsBoundDevice => IsRunning != null;

        ObservableDomainSidekick ISidekickLocator.Sidekick => _bridge.Sidekick;

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> if this observable object device is not bound
        /// to a <see cref="IDevice"/>.
        /// Note that <see cref="DeviceStatus"/> and <see cref="Configuration"/> are both null if this device is unbound.
        /// </summary>
        [MemberNotNull( nameof( Configuration ) )]
        [MemberNotNull( nameof( IsRunning ) )]
        public void ThrowOnUnboundedDevice()
        {
            if( !IsBoundDevice )
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
        /// To take full control of a newly created device, <see cref="DeviceConfiguration.ControllerKey"/> can be
        /// set to this <see cref="DomainView.DomainName"/>.
        /// </para>
        /// </summary>
        public void ApplyDeviceConfiguration( DeviceConfiguration configuration )
        {
            Throw.CheckNotNullArgument( configuration );
            if( configuration.Name == null ) configuration.Name = DeviceName;
            else Throw.CheckArgument( configuration.Name == DeviceName );
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

        internal class ForceSendCommand
        {
            public ForceSendCommand( BaseSetControllerKeyDeviceCommand setControllerKeyCommand ) => SetControllerKeyCommand = setControllerKeyCommand;
            public readonly BaseSetControllerKeyDeviceCommand SetControllerKeyCommand;
        }


        /// <summary>
        /// Sends a command to release or take the control of the device. See <see cref="DeviceControlAction"/>.
        /// </summary>
        /// <param name="action">The action to take.</param>
        public void SendDeviceControlCommand( DeviceControlAction action )
        {
            ThrowOnUnboundedDevice();
            _wantPersistentOwnership = action == DeviceControlAction.TakePersistentOwnership;
            switch( action )
            {
                case DeviceControlAction.SafeReleaseControl:
                {
                    // Nothing special to do: the set controller key command does this
                    // and our controller key must be honored.
                    var cmd = _bridge.CreateSetControllerKeyCommand();
                    Debug.Assert( cmd.NewControllerKey == null );
                    SendBasicCommand( cmd );
                    break;
                }
                case DeviceControlAction.SafeTakeControl:
                {
                    // Nothing special to do: the set controller key command does this
                    // and our controller key must be honored.
                    var cmd = _bridge.CreateSetControllerKeyCommand();
                    cmd.NewControllerKey = Domain.DomainName;
                    SendBasicCommand( cmd );
                    break;
                }
                case DeviceControlAction.ForceReleaseControl:
                {
                    // This reconfigures the device only if needed.
                    if( Configuration.ControllerKey != null )
                    {
                        var c = Configuration.DeepClone();
                        c.ControllerKey = null;
                        ApplyDeviceConfiguration( c );
                    }
                    else
                    {
                        // Uses the ReleaseControl that sends an unsafe command.
                        Domain.Monitor.Info( $"The device '{DeviceName}' is not controlled by its configuration. Using ReleaseControl action instead of reconfiguring via ForceReleaseControl." );
                        action = DeviceControlAction.ReleaseControl;
                    }
                    break;
                }
                case DeviceControlAction.TakeOwnership:
                case DeviceControlAction.TakePersistentOwnership:
                {
                    // This reconfigures the device only if needed.
                    if( Configuration.ControllerKey != Domain.DomainName )
                    {
                        var c = Configuration.DeepClone();
                        c.ControllerKey = Domain.DomainName;
                        ApplyDeviceConfiguration( c );
                    }
                    else
                    {
                        // Uses the TakeControl that sends an unsafe command.
                        Domain.Monitor.Info( $"The device '{DeviceName}' is already owned by this device. Using TakeControl action instead of reconfiguring via {action}." );
                        action = DeviceControlAction.TakeControl;
                    }
                    break;
                }
            }
            if( action == DeviceControlAction.TakeControl || action == DeviceControlAction.ReleaseControl )
            {
                var cmd = _bridge.CreateSetControllerKeyCommand();
                cmd.ControllerKey = Domain.DomainName;
                cmd.DeviceName = DeviceName;
                if( action == DeviceControlAction.TakeControl ) cmd.NewControllerKey = Domain.DomainName;
                Domain.SendCommand( new ForceSendCommand( cmd ), _bridge );
            }
        }

        void SendBasicCommand( BaseDeviceCommand cmd, bool unsafeSend = false )
        {
            cmd.ControllerKey = Domain.DomainName;
            cmd.DeviceName = DeviceName;
            Domain.SendCommand( cmd, _bridge );
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
            _isRunningChanged.RemoveAll();
            // Using nullable just in case EnsureDomainSidekick has not been called.
            ((IInternalObservableDeviceSidekick?)_bridge?.Sidekick)?.OnObjectDestroyed( Domain.Monitor, this );
            base.OnDestroy();
        }

        internal static DeviceControlStatus ComputeStatus( IDevice? d, string domainName, bool choosePersistentOwnership = false )
        {
            if( d == null ) return DeviceControlStatus.MissingDevice;
            string? controllerKey = d.ControllerKey;
            string? configKey = d.ExternalConfiguration.ControllerKey;

            return ComputeStatus( d.ControllerKey, d.ExternalConfiguration.ControllerKey, domainName, choosePersistentOwnership );
        }

        internal static DeviceControlStatus ComputeStatus( string? controllerKey, string? configKey, string domainName, bool choosePersistentOwnership )
        {
            if( controllerKey == null ) return DeviceControlStatus.HasSharedControl;
            if( controllerKey == domainName )
            {
                if( configKey == domainName ) return choosePersistentOwnership
                                                      ? DeviceControlStatus.HasPersistentOwnership
                                                      : DeviceControlStatus.HasOwnership;
                return DeviceControlStatus.HasControl;
            }
            return configKey != null
                    ? DeviceControlStatus.OutOfControlByConfiguration
                    : DeviceControlStatus.OutOfControl;
        }
    }
}
