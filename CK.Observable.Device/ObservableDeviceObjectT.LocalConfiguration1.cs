using CK.Core;
using CK.DeviceModel;
using CK.IO.ObservableDevice;

namespace CK.Observable.Device;

public abstract partial class ObservableDeviceObject<TSidekick, TConfig> : ILocalConfiguration<TConfig> where TSidekick : ObservableDomainSidekick
    where TConfig : DeviceConfiguration, new()
{
    private protected override DeviceConfiguration GetLocalConfiguration() => _localConfiguration;
    private protected override void SetLocalConfiguration( DeviceConfiguration value ) => DoSetLocalConfiguraton( (TConfig)value );

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
        if( value != _localConfiguration )
        {
            if( value == _deviceConfiguration )
            {
                _localConfiguration = (TConfig)_deviceConfiguration.DeepClone();
                _localConfiguration.CheckValid( Domain.Monitor ); // CheckValid() HAS INTERNAL SIDE-EFFECTS AND MUST BE CALLED AFTER DeepClone().
            }
            else
            {
                _localConfiguration = value;
            }
            base.LocalConfiguration.UpdateIsDirty();
        }
    }

    bool ILocalConfiguration<TConfig>.IsDirty => base.LocalConfiguration.IsDirty;

    bool ILocalConfiguration<TConfig>.UpdateIsDirty() => base.LocalConfiguration.UpdateIsDirty();

    event SafeEventHandler ILocalConfiguration<TConfig>.IsDirtyChanged
    {
        add => base.LocalConfiguration.IsDirtyChanged += value;
        remove => base.LocalConfiguration.IsDirtyChanged -= value;
    }

    void ILocalConfiguration<TConfig>.SendDeviceConfigureCommand( DeviceControlAction? deviceControlAction ) => base.LocalConfiguration.SendDeviceConfigureCommand( deviceControlAction );


}
