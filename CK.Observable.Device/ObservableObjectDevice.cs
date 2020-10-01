using CK.Core;
using CK.DeviceModel;
using CK.DeviceModel.Command;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable.Device
{
    /// <summary>
    /// Non generic abstract base class for device that is not intended to be specialized directly.
    /// Use the generic <see cref="ObservableObjectDevice{TSidekick}"/> as the object device base.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableObjectDevice : ObservableObject
    {
        private protected ObservableObjectDevice( string deviceName )
        {
            if( deviceName == null ) throw new ArgumentNullException( nameof( deviceName ) );
            DeviceName = deviceName;
        }

        private protected ObservableObjectDevice( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            var r = ctx.StartReading().Reader;
            DeviceName = r.ReadNullableString();
        }

        void Write( BinarySerializer s )
        {
            s.WriteNullableString( DeviceName );
        }

        internal interface IBridge
        {
            BasicControlDeviceCommand CreateBasicCommand();

            IEnumerable<string> CurrentlyAvailableDeviceNames { get; }

            string? ControllerKey { get; }

            T CreateCommand<T>( Action<T>? configuration ) where T : DeviceCommand, new();
        }

        internal IBridge _bridge;

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
        /// Gets whether the device is under control of this object or the <see cref="IDevice.ControllerKey"/> is null: the device
        /// doesn't restrict the commands.
        /// </summary>
        public bool HasDeviceControl { get; internal set; }

        /// <summary>
        /// Gets whether the device is under control of this object, excluding the other ones.
        /// </summary>
        public bool HasExclusiveDeviceControl { get; internal set; }

        /// <summary>
        /// Gets whether this observable object device is bound to a <see cref="IDevice"/>.
        /// Note that <see cref="Status"/> and <see cref="ConfigurationStatus"/> are both null if this device is unbound and
        /// that this flag is [NotExportable].
        /// </summary>
        [NotExportable]
        public bool IsBoundDevice => Status != null;

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> if this observable object device is not bound
        /// to a <see cref="IDevice"/>.
        /// Note that <see cref="Status"/> and <see cref="ConfigurationStatus"/> are both null if this device is unbound.
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
        /// Sends a start command to the device.
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </summary>
        /// <param name="autoEnsureControl">Set to true to call <see cref="CmdEnsureDeviceControl"/> first.</param>
        public void CmdStart( bool autoEnsureControl = false )
        {
            ThrowOnUnboundedDevice();
            if( autoEnsureControl ) CmdEnsureDeviceControl();
            SendBasicCommand( BasicControlDeviceOperation.Start );
        }

        /// <summary>
        /// Sends a stop command to the device.
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </summary>
        /// <param name="autoEnsureControl">Set to true to call <see cref="CmdEnsureDeviceControl"/> first.</param>
        public void CmdStop( bool autoEnsureControl = false )
        {
            ThrowOnUnboundedDevice();
            if( autoEnsureControl ) CmdEnsureDeviceControl();
            SendBasicCommand( BasicControlDeviceOperation.Stop );
        }

        /// <summary>
        /// Sends a command to take the control of the device if <see cref="HasDeviceControl"/> is false.
        /// This command and <see cref="CmdReleaseDeviceControl"/> are the only commands that don't require <see cref="HasDeviceControl"/> to be true.
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </summary>
        public void CmdEnsureDeviceControl()
        {
            ThrowOnUnboundedDevice();
            if( !HasDeviceControl ) SendBasicCommand( BasicControlDeviceOperation.ResetControllerKey );
        }

        /// <summary>
        /// Sends a command to release the control of the device.
        /// This is the only command with <see cref="CmdEnsureDeviceControl"/> that doesn't require <see cref="HasDeviceControl"/> to be true: nothing is done
        /// if HasDeviceControl is false.
        /// Caution: <see cref="ThrowOnUnboundedDevice"/> is called, <see cref="IsBoundDevice"/> must be true before calling this.
        /// </summary>
        public void CmdReleaseDeviceControl()
        {
            ThrowOnUnboundedDevice();
            if( HasDeviceControl ) SendBasicCommand( BasicControlDeviceOperation.ResetControllerKey, true );
        }

        void SendBasicCommand( BasicControlDeviceOperation operation, bool nullControllerKey = false )
        {
            var c = _bridge.CreateBasicCommand();
            c.DeviceName = DeviceName;
            c.Operation = operation;
            c.ControllerKey = nullControllerKey ? null : _bridge.ControllerKey;
            Domain.SendCommand( c );
        }

        /// <summary>
        /// Creates a new command of the given type with its <see cref="DeviceCommand.DeviceName"/> and <see cref="DeviceCommand.ControllerKey"/>.
        /// Note that if the <see cref="DeviceCommand.HostType"/> is not compatible with the actual <see cref="IDeviceHost"/> of this
        /// <see cref="ObservableObjectDevice{TSidekick}"/> sidekick, a <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <typeparam name="T">The type of the command to create.</typeparam>
        /// <param name="configuration">Optional configurator of the command.</param>
        /// <returns>A ready to use command.</returns>
        protected T CreateCommand<T>( Action<T>? configuration = null ) where T : DeviceCommand, new() => _bridge.CreateCommand<T>( configuration );

        /// <summary>
        /// Simple helper that calls <see cref="CreateCommand{T}(Action{T}?)"/> and <see cref="DomainView.SendCommand(object)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the command to create.</typeparam>
        /// <param name="configuration">Optional configurator of the command.</param>
        protected void CmdSend<T>( Action<T>? configuration = null ) where T : DeviceCommand, new()
        {
            Domain.SendCommand( CreateCommand( configuration ) );
        }

    }
}
