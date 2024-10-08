using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using System.Diagnostics;
using System.Text;
using System;
using Microsoft.IO;

namespace CK.Observable.Device;

public abstract partial class ObservableDeviceObject : ILocalConfiguration<DeviceConfiguration>
{
    /// <summary>
    /// Gets the local configuration.
    /// </summary>
    public ILocalConfiguration<DeviceConfiguration> LocalConfiguration => this;

    DeviceConfiguration ILocalConfiguration<DeviceConfiguration>.Value
    {
        get => GetLocalConfiguration();
        set => SetLocalConfiguration( value );
    }

    /// <summary>
    /// It is the <see cref="ObservableDeviceObject{TSidekick, TConfig}"/> that carries the
    /// local configuration.
    /// </summary>
    private protected abstract DeviceConfiguration GetLocalConfiguration();
    private protected abstract void SetLocalConfiguration( DeviceConfiguration value );

    bool ILocalConfiguration<DeviceConfiguration>.IsDirty => _isLocalConfigurationDirty;

    event SafeEventHandler ILocalConfiguration<DeviceConfiguration>.IsDirtyChanged
    {
        add => _isDirtyChanged.Add( value );
        remove => _isDirtyChanged.Remove( value );
    }

    bool ILocalConfiguration<DeviceConfiguration>.UpdateIsDirty() => UpdateConfigurationIsDirty();

    bool UpdateConfigurationIsDirty()
    {
        Throw.CheckState( Domain.CurrentTransactionStatus == CurrentTransactionStatus.Regular );

        var local = GetLocalConfiguration();
        if( _deviceConfiguration == null || !local.CheckValid( Domain.Monitor ) )
        {
            if( !_isLocalConfigurationDirty )
            {
                _isLocalConfigurationDirty = true;
                _isDirtyChanged.Raise( _isLocalConfigurationDirty );
            }
            return true;
        }
        var newDirty = ComputeDirty( local, _deviceConfiguration );
        if( newDirty != _isLocalConfigurationDirty )
        {
            _isLocalConfigurationDirty = newDirty;
            _isDirtyChanged.Raise( _isLocalConfigurationDirty );
        }
        return _isLocalConfigurationDirty;

        static bool ComputeDirty( DeviceConfiguration local, DeviceConfiguration current )
        {
            using( var s = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream() )
            using( var w = new CKBinaryWriter( s, Encoding.UTF8 ) )
            {
                local.Write( w );
                using var checker = CheckedWriteStream.Create( s );
                using var checkedWriter = new CKBinaryWriter( checker, Encoding.UTF8 );
                current.Write( checkedWriter );
                return checker.GetResult() != CheckedWriteStream.Result.None;
            }
        }
    }

    void ILocalConfiguration<DeviceConfiguration>.SendDeviceConfigureCommand( DeviceControlAction? deviceControlAction )
    {
        var status = _deviceControlStatus;
        var local = GetLocalConfiguration();
        using( Domain.Monitor.OpenInfo( $"Applying Local configuration: Actual Status : {status}, Action: {deviceControlAction}" ) )
        {
            if( !local.CheckValid( Domain.Monitor ) ) Throw.InvalidOperationException( "Local configuration is invalid" );

            var shouldApply = status == DeviceControlStatus.MissingDevice;
            switch( deviceControlAction )
            {
                case DeviceControlAction.TakeControl:
                case DeviceControlAction.ReleaseControl:
                    if( status != DeviceControlStatus.OutOfControlByConfiguration )
                    {
                        shouldApply = true;
                    }
                    break;
                case DeviceControlAction.ForceReleaseControl:
                    if( status != DeviceControlStatus.MissingDevice )
                    {
                        Debug.Assert( _deviceConfiguration != null );
                        if( _deviceConfiguration.ControllerKey != null )
                        {
                            local.ControllerKey = null;
                            shouldApply = true;
                        }
                    }
                    else
                    {
                        local.ControllerKey = null;
                        shouldApply = true;
                    }

                    break;
                case DeviceControlAction.SafeTakeControl:
                    if( status == DeviceControlStatus.HasControl || status == DeviceControlStatus.HasSharedControl )
                    {
                        shouldApply = true;
                    }
                    break;
                case DeviceControlAction.SafeReleaseControl:
                    if( status == DeviceControlStatus.HasControl || status == DeviceControlStatus.HasSharedControl )
                    {
                        shouldApply = true;
                    }
                    break;
                case DeviceControlAction.TakeOwnership:
                    if( status != DeviceControlStatus.MissingDevice )
                    {
                        Debug.Assert( _deviceConfiguration != null );
                        if( _deviceConfiguration.ControllerKey != Domain.DomainName )
                        {
                            local.ControllerKey = Domain.DomainName;
                            shouldApply = true;
                        }
                    }
                    else
                    {
                        local.ControllerKey = Domain.DomainName;
                        shouldApply = true;
                    }
                    break;
                case null:
                    shouldApply = status == DeviceControlStatus.HasControl
                               || status == DeviceControlStatus.HasSharedControl
                               || status == DeviceControlStatus.MissingDevice
                               || status == DeviceControlStatus.HasOwnership;
                    break;
            }

            if( shouldApply )
            {
                if( status == DeviceControlStatus.MissingDevice )
                {
                    if( deviceControlAction == DeviceControlAction.TakeControl ||
                        deviceControlAction == DeviceControlAction.SafeTakeControl
                       )
                    {
                        var setControllerKeyCommand = _bridge.CreateSetControllerKeyCommand();
                        setControllerKeyCommand.ControllerKey = Domain.DomainName;
                        setControllerKeyCommand.DeviceName = DeviceName;
                        setControllerKeyCommand.NewControllerKey = Domain.DomainName;

                        var configClone = local.DeepClone();
                        configClone.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
                        var applyAndSetControllerKeyCommand = new ApplyAndSetControllerKeyDeviceCommand( setControllerKeyCommand, configClone );

                        Domain.SendCommand( applyAndSetControllerKeyCommand, _bridge );
                    }
                    else if( deviceControlAction == DeviceControlAction.TakeOwnership || deviceControlAction == null )
                    {
                        var configClone = local.DeepClone();
                        configClone.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
                        SendApplyDeviceConfigurationCommand( configClone );
                    }
                }
                else
                {
                    if( deviceControlAction != null )
                    {
                        SendDeviceControlCommand( deviceControlAction.Value, local );
                    }

                    if( deviceControlAction != DeviceControlAction.TakeOwnership ||
                        deviceControlAction != DeviceControlAction.ForceReleaseControl )
                    {
                        var configClone = local.DeepClone();
                        configClone.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
                        SendApplyDeviceConfigurationCommand( configClone );
                    }
                }

            }
            else
            {
                Domain.Monitor.CloseGroup( "Inapplicable Action." );
            }
        }
    }
}
