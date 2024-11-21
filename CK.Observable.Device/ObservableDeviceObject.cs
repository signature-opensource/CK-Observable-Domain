using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace CK.Observable.Device;

/// <summary>
/// Non generic abstract base class for device. It is not intended to be specialized directly: use the
/// generic <see cref="ObservableDeviceObject{TSidekick,TConfig}"/> as the object device base.
/// </summary>
[SerializationVersion( 4 )]
public abstract partial class ObservableDeviceObject : ObservableObject, ISidekickLocator
{
    ObservableEventHandler _isRunningChanged;
    ObservableEventHandler _deviceConfigurationChanged;
    ObservableEventHandler _deviceControlStatusChanged;
    ObservableEventHandler _isDirtyChanged;
    internal IInternalDeviceBridge _bridge;
    internal DeviceConfiguration? _deviceConfiguration;
    internal DeviceControlStatus _deviceControlStatus;
    internal bool? _isRunning;
    internal bool _isLocalConfigurationDirty;
    internal bool _hasConfiguredLocalOnce;

#pragma warning disable CS8618 // Non-nullable _bridge uninitialized. Consider declaring as nullable.

    private protected ObservableDeviceObject( string deviceName )
    {
        Throw.CheckNotNullArgument( deviceName );
        DeviceName = deviceName;
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    protected ObservableDeviceObject( Sliced _ ) : base( _ ) { }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    ObservableDeviceObject( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        DeviceName = info.Version == 0 ? d.Reader.ReadNullableString()! : d.Reader.ReadString();
        _isRunningChanged = new ObservableEventHandler( d );
        if( info.Version > 0 )
        {
            _deviceConfigurationChanged = new ObservableEventHandler( d );
        }
        if( info.Version > 1 )
        {
            _deviceControlStatusChanged = new ObservableEventHandler( d );
            _isRunning = d.Reader.ReadNullableBool();
            _deviceControlStatus = (DeviceControlStatus)d.Reader.ReadByte();
            if( info.Version == 2 )
            {
                // Was _wantPersistentOwnership
                d.Reader.ReadBoolean();
            }

        }

        if( info.Version > 3 )
        {
            _isLocalConfigurationDirty = d.Reader.ReadBoolean();
            _isDirtyChanged = new ObservableEventHandler( d );
            _hasConfiguredLocalOnce = d.Reader.ReadBoolean();
        }
    }

    public static void Write( IBinarySerializer d, in ObservableDeviceObject o )
    {
        d.Writer.Write( o.DeviceName );
        o._isRunningChanged.Write( d );
        o._deviceConfigurationChanged.Write( d );
        o._deviceControlStatusChanged.Write( d );
        d.Writer.WriteNullableBool( o._isRunning );
        d.Writer.Write( (byte)o._deviceControlStatus );

        // version 4
        d.Writer.Write( o._isLocalConfigurationDirty );
        o._isDirtyChanged.Write( d );
        d.Writer.Write( o._hasConfiguredLocalOnce );

    }

#pragma warning restore CS8618

    internal interface IInternalDeviceBridge : ISidekickLocator
    {
        BaseConfigureDeviceCommand CreateConfigureCommand( DeviceConfiguration? configuration );

        BaseStartDeviceCommand CreateStartCommand();

        BaseStopDeviceCommand CreateStopCommand();

        BaseDestroyDeviceCommand CreateDestroyCommand();

        BaseSetControllerKeyDeviceCommand CreateSetControllerKeyCommand();

        IDevice? Device { get; }

        IEnumerable<string> CurrentlyAvailableDeviceNames { get; }

        T CreateCommand<T>( Action<T>? configuration ) where T : BaseDeviceCommand, new();
    }

    /// <summary>
    /// Gets the name of this device.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Gets the actual device if it exists or null.
    /// </summary>
    /// <remarks>
    /// This is weakly typed and protected because direct interactions with devices from the Observable
    /// layer should not occur: the commands and events handled by the sidekick must do he job.
    /// However since some use case require the actual device (like creating events), it is exposed here.
    /// </remarks>
    protected IDevice? Device => _bridge?.Device;

    /// <summary>
    /// Gets the device status.
    /// This is null when no device named <see cref="DeviceName"/> exist in the device host.
    /// </summary>
    public bool? IsRunning => _isRunning;

    /// <summary>
    /// Called when <see cref="IsRunning"/> changed.
    /// <para>
    /// When overridden, this base method MUST be called to raise <see cref="IsRunningChanged"/> event.
    /// </para>
    /// </summary>
    internal protected virtual void OnIsRunningChanged()
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
    /// even if it can, this instance MUST not be modified.
    /// </para>
    /// <para>
    /// Use the the <see cref="LocalConfiguration"/> to manage device configuration.
    /// </para>
    /// </summary>
    [NotExportable]
    public DeviceConfiguration? DeviceConfiguration => _deviceConfiguration;


    internal void InternalDeviceConfigurationChanged( DeviceConfiguration? previousDeviceConfiguration )
    {
        if( !_hasConfiguredLocalOnce )
        {
            Debug.Assert( _deviceConfiguration != null );
            LocalConfiguration.Value = _deviceConfiguration;
            _hasConfiguredLocalOnce = true;
        }
        // First raise the DeviceConfigurationChanged event.
        OnDeviceConfigurationChanged( previousDeviceConfiguration );
        // Then check the local IsDirty flag.
        UpdateConfigurationIsDirty();
    }

    /// <summary>
    /// Called when <see cref="DeviceConfiguration"/> changed.
    /// <para>
    /// When overridden, this base method MUST be called to raise <see cref="DeviceConfigurationChanged"/> event.
    /// </para>
    /// </summary>
    protected virtual void OnDeviceConfigurationChanged( DeviceConfiguration? previousDeviceConfiguration )
    {
        OnPropertyChanged( nameof( DeviceConfiguration ), _deviceConfiguration );
        if( _deviceConfigurationChanged.HasHandlers ) _deviceConfigurationChanged.Raise( this );
    }

    /// <summary>
    /// Raised whenever <see cref="DeviceConfiguration"/> has changed.
    /// </summary>
    public event SafeEventHandler DeviceConfigurationChanged
    {
        add => _deviceConfigurationChanged.Add( value );
        remove => _deviceConfigurationChanged.Remove( value );
    }

    /// <summary>
    /// Gets this device control status.
    /// Uses <see cref="SendDeviceControlCommand(DeviceControlAction, DeviceConfiguration?)"/> to change how
    /// this device object controls the actual device.
    /// </summary>
    public DeviceControlStatus DeviceControlStatus => _deviceControlStatus;

    /// <summary>
    /// Called when <see cref="DeviceControlStatus"/> changed.
    /// <para>
    /// When overridden, this base method MUST be called to raise <see cref="DeviceControlStatusChanged"/> event.
    /// </para>
    /// </summary>
    internal protected virtual void OnDeviceControlStatusChanged()
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
    /// Note that <see cref="IsRunning"/> and <see cref="DeviceConfiguration"/> are both null if this device is unbound.
    /// This flag is [NotExportable].
    /// </summary>
    [NotExportable]
    [MemberNotNullWhen( true, nameof( DeviceConfiguration ), nameof( IsRunning ) )]
    public bool IsBoundDevice => IsRunning != null;

    ObservableDomainSidekick ISidekickLocator.Sidekick => _bridge.Sidekick;

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if this observable object device is not bound
    /// to a <see cref="IDevice"/>.
    /// Note that <see cref="DeviceStatus"/> and <see cref="DeviceConfiguration"/> are both null if this device is unbound.
    /// </summary>
    [MemberNotNull( nameof( DeviceConfiguration ) )]
    [MemberNotNull( nameof( IsRunning ) )]
    public void ThrowOnUnboundedDevice()
    {
        if( !IsBoundDevice )
        {
            var msg = $"Device '{DeviceName}' of type '{GetType().Name}' is not bound to any device. Available device names: '{_bridge.CurrentlyAvailableDeviceNames.Concatenate( "', '" )}'.";
            Throw.InvalidOperationException( msg );
        }
    }

    void SendApplyDeviceConfigurationCommand( DeviceConfiguration configuration )
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

    internal class ApplyAndSetControllerKeyDeviceCommand
    {
        public ApplyAndSetControllerKeyDeviceCommand(
            BaseSetControllerKeyDeviceCommand setControllerKeyCommand,
            DeviceConfiguration deviceConfiguration
        )
        {
            SetControllerKeyCommand = setControllerKeyCommand;
            DeviceConfiguration = deviceConfiguration;
        }
        public readonly BaseSetControllerKeyDeviceCommand SetControllerKeyCommand;
        public readonly DeviceConfiguration DeviceConfiguration;
    }


    /// <summary>
    /// Sends a command to release or take the control of the device. See <see cref="DeviceControlAction"/>.
    /// </summary>
    /// <param name="action">The action to take.</param>
    public void SendDeviceControlCommand( DeviceControlAction action, DeviceConfiguration? newConfig = null )
    {
        ThrowOnUnboundedDevice();
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
                    if( DeviceConfiguration.ControllerKey != null )
                    {
                        var c = newConfig != null ? newConfig.DeepClone() : DeviceConfiguration.DeepClone();
                        c.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
                        c.ControllerKey = null;
                        SendApplyDeviceConfigurationCommand( c );
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
                {
                    // This reconfigures the device only if needed.
                    if( DeviceConfiguration.ControllerKey != Domain.DomainName )
                    {
                        var c = newConfig != null ? newConfig.DeepClone() : DeviceConfiguration.DeepClone();
                        c.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
                        c.ControllerKey = Domain.DomainName;
                        SendApplyDeviceConfigurationCommand( c );
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
    /// Creates a new command of the given type with its <see cref="BaseDeviceCommand.DeviceName"/> and <see cref="BaseDeviceCommand.ControllerKey"/>.
    /// Note that if the <see cref="BaseDeviceCommand.HostType"/> is not compatible with the actual <see cref="IDeviceHost"/> of this
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

    /// <summary>
    /// Overridden to synchronize the sidekick before calling base <see cref="ObservableObject.OnDestroy()"/>.
    /// </summary>
    protected override void OnDestroy()
    {
        _deviceConfigurationChanged.RemoveAll();
        _isRunningChanged.RemoveAll();
        _isDirtyChanged.RemoveAll();
        // Using nullable just in case EnsureDomainSidekick has not been called.
        ((IInternalObservableDeviceSidekick?)_bridge?.Sidekick)?.OnObjectDestroyed( Domain.Monitor, this );
        base.OnDestroy();
    }

    internal static DeviceControlStatus ComputeStatus( IDevice? d, string domainName )
    {
        if( d == null ) return DeviceControlStatus.MissingDevice;
        return ComputeStatus( d.ControllerKey, d.ExternalConfiguration.ControllerKey, domainName );
    }

    internal static DeviceControlStatus ComputeStatus( string? controllerKey, string? configKey, string domainName )
    {
        if( controllerKey == null ) return DeviceControlStatus.HasSharedControl;
        if( controllerKey == domainName )
        {
            return configKey == domainName
                                ? DeviceControlStatus.HasOwnership
                                : DeviceControlStatus.HasControl;
        }
        return configKey != null
                ? DeviceControlStatus.OutOfControlByConfiguration
                : DeviceControlStatus.OutOfControl;
    }

}
