using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Device;

/// <summary>
/// Observable informations on devices exposed by <see cref="ObservableDeviceHostObject{TSidekick, TObservableDeviceObject}.Devices"/>.
/// </summary>
[SerializationVersion(0)]
public sealed class ODeviceInfo<TDeviceObject> : ObservableObject where TDeviceObject : ObservableDeviceObject, ICKSlicedSerializable
{
    TDeviceObject? _object;
    DeviceControlStatus _status;
    string? _controllerKey;
    string? _configControllerKey;
    bool _isRunning;

    // The observable exists and the device may exist. 
    internal ODeviceInfo( TDeviceObject o, IDevice? d )
    {
        DeviceName = o.DeviceName;
        Object = o;
        _status = o.DeviceControlStatus;
        _controllerKey = d?.ControllerKey;
        _configControllerKey = d?.ExternalConfiguration.ControllerKey;
        _isRunning = o.IsRunning == true;
    }

    // The device exists and the observable may exist.
    internal ODeviceInfo( IDevice device, TDeviceObject? o )
    {
        DeviceName = device.Name;
        Object = o;
        _status = o != null ? o.DeviceControlStatus : ObservableDeviceObject.ComputeStatus( device, Domain.DomainName );
        _controllerKey = device.ControllerKey;
        _configControllerKey = device.ExternalConfiguration.ControllerKey;
        _isRunning = device.IsRunning;
    }

    ODeviceInfo( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        DeviceName = d.Reader.ReadString();
        Object = d.ReadNullableObject<TDeviceObject>();
        _status = (DeviceControlStatus)d.Reader.ReadByte();
        _controllerKey = d.Reader.ReadNullableString();
        _configControllerKey = d.Reader.ReadNullableString();
        _isRunning = d.Reader.ReadBoolean();
    }

    public static void Write( IBinarySerializer s, in ODeviceInfo<TDeviceObject> o )
    {
        s.Writer.Write( o.DeviceName );
        s.WriteNullableObject( o.Object );
        s.Writer.Write( (byte)o._status );
        s.Writer.WriteNullableString( o._controllerKey );
        s.Writer.WriteNullableString( o._configControllerKey );
        s.Writer.Write( o._isRunning );
    }

    /// <summary>
    /// Gets the name of the device.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Gets the observable device object if it has been instantiated.
    /// </summary>
    public TDeviceObject? Object
    {
        get => _object;
        internal set
        {
            if( _object != value )
            {
                _object = value;
                OnPropertyChanged( nameof( Object ), value );
            }
        }
    }

    /// <summary>
    /// Gets whether the device control status for this domain.
    /// </summary>
    public DeviceControlStatus Status
    {
        get => _status;
        internal set
        {
            if( _status != value )
            {
                _status = value;
                OnPropertyChanged( nameof( Status ), value );
            }
        }
    }

    /// <summary>
    /// Gets whether the device is currently running.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        internal set
        {
            if( _isRunning != value )
            {
                _isRunning = value;
                OnPropertyChanged( nameof( IsRunning ), value );
            }
        }
    }

    /// <summary>
    /// Gets the current device's controller key.
    /// </summary>
    public string? ControllerKey
    {
        get => _controllerKey;
        internal set
        {
            if( _controllerKey != value )
            {
                _controllerKey = value;
                OnPropertyChanged( nameof( ControllerKey ), value );
            }
        }
    }

    /// <summary>
    /// Gets the current device's configured controller key.
    /// </summary>
    public string? ConfigurationControllerKey
    {
        get => _configControllerKey;
        internal set
        {
            if( _configControllerKey != value )
            {
                _configControllerKey = value;
                OnPropertyChanged( nameof( ConfigurationControllerKey ), value );
            }
        }
    }

    public override string ToString() => $"Device: {DeviceName} [{(IsRunning ? "Running" : "Stopped")}, {Status}]";
}
