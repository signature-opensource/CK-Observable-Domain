using CK.Core;
using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable.Device
{
    public abstract partial class ObservableDeviceSidekick<THost,TDeviceObject,TDeviceHostObject> 
    {
        /// <summary>
        /// Must create a <see cref="Bridge{TSidekick, TDevice}"/> between <typeparamref name="TDeviceObject"/> and its actual <see cref="Bridge{TSidekick, TDevice}.Device"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The object to be bridged.</param>
        /// <returns>The bridge for the observable device object.</returns>
        protected abstract DeviceBridge CreateBridge( IActivityMonitor monitor, TDeviceObject o );

        /// <summary>
        /// Gets the bridge that has been associated to the <paramref name="deviceObject"/>.
        /// </summary>
        /// <param name="deviceObject">The device object.</param>
        /// <returns>The bridge (that should certainly be down casted).</returns>
        protected DeviceBridge FindBridge( TDeviceObject deviceObject ) => _bridges[deviceObject.DeviceName];

        /// <summary>
        /// Non generic bridge between observable <typeparamref name="TDeviceObject"/> and its actual <see cref="Device"/>.
        /// This cannot be directly instantiated: the generic <see cref="Bridge{TSidekick,TDevice}"/> adapter must be used.
        /// </summary>
        protected abstract class DeviceBridge : ObservableDeviceObject.IDeviceBridge
        {
            internal ObservableDeviceSidekick<THost,TDeviceObject,TDeviceHostObject> _sidekick;
            internal DeviceBridge? _nextUnbound;

            /// <summary>
            /// Gets the observable object device.
            /// </summary>
            internal protected TDeviceObject Object { get; }

            /// <summary>
            /// Gets the associated device or null if no actual device with the <see cref="ObservableDeviceObject.DeviceName"/> exists in the host.
            /// </summary>
            internal protected IDevice? Device { get; private set; }

            #pragma warning disable CS8618 // Non-nullable _sidekick uninitialized. Consider declaring as nullable.
            /// <summary>
            /// This is private protected so that developers are obliged to use the <see cref="Bridge{TSidekick,TDevice}"/> generic adapter.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            private protected DeviceBridge( TDeviceObject o )
            {
                Debug.Assert( !o.IsDestroyed );
                Object = o;
                o._bridge = this;
            }
            #pragma warning restore CS8618

            ObservableDomainSidekick ObservableDeviceObject.IDeviceBridge.Sidekick => _sidekick;

            BaseStartDeviceCommand ObservableDeviceObject.IDeviceBridge.CreateStartCommand() => new StartDeviceCommand<THost>();
            BaseStopDeviceCommand ObservableDeviceObject.IDeviceBridge.CreateStopCommand() => new StopDeviceCommand<THost>();
            BaseDestroyDeviceCommand ObservableDeviceObject.IDeviceBridge.CreateDestroyCommand() => new DestroyDeviceCommand<THost>();
            BaseSetControllerKeyDeviceCommand ObservableDeviceObject.IDeviceBridge.CreateSetControllerKeyCommand() => new SetControllerKeyDeviceCommand<THost>();

            IEnumerable<string> ObservableDeviceObject.IDeviceBridge.CurrentlyAvailableDeviceNames => _sidekick._objectHost?.Devices.Select( d => d.Name )
                                                                                                        ?? _sidekick.Host.GetConfiguredDevices().Select( d => d.Item2.Name );

            string? ObservableDeviceObject.IDeviceBridge.ControllerKey => Device?.ControllerKey;

            internal void Initialize( IActivityMonitor monitor, ObservableDeviceSidekick<THost, TDeviceObject, TDeviceHostObject> owner, IDevice? initialDevice )
            {
                _sidekick = owner;
                if( initialDevice == null ) owner.AddUnbound( this );
                else
                {
                    SetDevice( monitor, initialDevice, initCall: true );
                }
            }

            internal void SetDevice( IActivityMonitor monitor, IDevice d, bool initCall = false )
            {
                Debug.Assert( Device == null, "This is called only if the current Device is null." );
                Device = d;
                Object.Status = d.Status;
                Object.ConfigurationStatus = d.ConfigurationStatus;
                SetObjectDeviceControlProperties( d.ControllerKey );

                d.StatusChanged.Async += OnDeviceStatusChanged;
                d.ControllerKeyChanged.Async += OnDeviceControllerKeyChanged;
                if( !initCall ) _sidekick.RemoveUnbound( this );
                OnDeviceAppeared( monitor );
            }

            void SetObjectDeviceControlProperties( string? deviceKey )
            {
                var n = _sidekick.Domain.DomainName;
                Object.HasDeviceControl = deviceKey == null || deviceKey == n;
                Object.HasExclusiveDeviceControl = deviceKey == n;
            }

            internal void DetachDevice( IActivityMonitor monitor )
            {
                Debug.Assert( Device != null, "This is called only if a Device is bound." );
                OnDeviceDisappearing( monitor );
                Object.Status = null;
                Object.ConfigurationStatus = null;
                Object.HasDeviceControl = false;
                Object.HasExclusiveDeviceControl = false;
                Device.StatusChanged.Async -= OnDeviceStatusChanged;
                Device.ControllerKeyChanged.Async -= OnDeviceControllerKeyChanged;
                _sidekick.AddUnbound( this );
                Device = null;
            }

            Task OnDeviceControllerKeyChanged( IActivityMonitor monitor, IDevice sender, string? controllerKey )
            {
                return ModifyAsync( monitor, () => SetObjectDeviceControlProperties( controllerKey ) );
            }

            Task OnDeviceStatusChanged( IActivityMonitor monitor, IDevice sender )
            {
                Debug.Assert( Device != null );
                return ModifyAsync( monitor, () =>
                {
                    if( sender.IsDestroyed )
                    {
                        Debug.Assert( sender.Status.IsDestroyed );
                        DetachDevice( monitor );
                    }
                    else
                    {
                        Debug.Assert( !sender.Status.IsDestroyed );
                        Object.Status = sender.Status;
                        Object.ConfigurationStatus = sender.ConfigurationStatus;
                        SetObjectDeviceControlProperties( sender.ControllerKey );
                    }
                } );
            }

            /// <summary>
            /// Called when this bridge must be disposed because either the <see cref="Object"/>
            /// is unloaded or destroyed.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="isObjectDestroyed">True when the object has been destroyed, false when it is only unloaded.</param>
            internal void OnDispose( IActivityMonitor monitor, bool isObjectDestroyed )
            {
                if( Device == null ) _sidekick.RemoveUnbound( this );
                else
                {
                    Device.StatusChanged.Async -= OnDeviceStatusChanged;
                    Device.ControllerKeyChanged.Async -= OnDeviceControllerKeyChanged;
                }
                OnObjectDisappeared( monitor, isObjectDestroyed );
            }

            /// <inheritdoc cref="ObservableDeviceObject.CreateDeviceCommand{T}(Action{T}?)" />
            public T CreateCommand<T>( Action<T>? configuration ) where T : BaseDeviceCommand, new()
            {
                var c = new T();
                if( !c.HostType.IsAssignableFrom( _sidekick.Host.GetType() ) )
                {
                    throw new InvalidOperationException( $"Command '{c.GetType().Name}' is bound to HostType '{c.HostType.Name}'. It cannot be handled by host {_sidekick.Host.GetType().Name}." );
                }
                c.DeviceName = Object.DeviceName;
                c.ControllerKey = Device?.ControllerKey;
                configuration?.Invoke( c );
                return c;
            }

            /// <inheritdoc cref="ObservableDomain.Modify(IActivityMonitor, Action, int)" />
            protected Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 ) => _sidekick.Domain.ModifyAsync( monitor, actions, millisecondsTimeout );

            /// <inheritdoc cref="ObservableDomain.ModifyNoThrowAsync(IActivityMonitor, Action, int, bool)"/>.
            protected Task<(Exception? OnStartTransactionError, TransactionResult Transaction)> ModifyNoThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 ) => _sidekick.Domain.ModifyNoThrowAsync( monitor, actions, millisecondsTimeout );

            /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action, int, bool)"/>.
            protected Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 ) => _sidekick.Domain.ModifyThrowAsync( monitor, actions, millisecondsTimeout );

            /// <summary>
            /// Called whenever the <see cref="Device"/> became not null.
            /// The <see cref="Object"/> (and any other objects of the domain) can be safely modified
            /// since the domain's write lock is held.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected abstract void OnDeviceAppeared( IActivityMonitor monitor );

            /// <summary>
            /// Called whenever the <see cref="Device"/> is no more available in the host: it is
            /// still not null here and events unregistering should be done.
            /// The observable <see cref="Object"/> (and any other objects of the domain) can
            /// be safely modified since the domain's write lock is held.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected abstract void OnDeviceDisappearing( IActivityMonitor monitor );

            /// <summary>
            /// Called whenever the <see cref="ObservableDeviceObject"/> is unloaded or destroyed.
            /// This method does nothing by default.
            /// <para>
            /// Note that the Device may continue to exist in its host, but this method may destroy the device in the Device world
            /// (if the observable <see cref="Object"/> must drive the life cycle of the device and <paramref name="isObjectDestroyed"/> is true).
            /// </para>
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="isObjectDestroyed">True when the object has been destroyed, false when it is only unloaded.</param>
            protected virtual void OnObjectDisappeared( IActivityMonitor monitor, bool isObjectDestroyed )
            {
            }
        }

        /// <summary>
        /// Base class to implement to bridge <typeparamref name="TDevice"/> to observable objects.
        /// Specialized classes have access to the observable object (<see cref="DeviceBridge.Object"/>),
        /// the device that may not exist (<see cref="Device"/>) and the <see cref="Sidekick"/> in a
        /// strongly typed manner.
        /// </summary>
        /// <typeparam name="TSidekick">The type of the sidekick that manages this bridge.</typeparam>
        /// <typeparam name="TDevice">The type of the actual device.</typeparam>
        protected abstract class Bridge<TSidekick,TDevice> : DeviceBridge
            where TSidekick : ObservableDeviceSidekick<THost, TDeviceObject, TDeviceHostObject>
            where TDevice : class, IDevice
        {
            /// <summary>
            /// Initializes a new bridge.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            protected Bridge( TDeviceObject o )
                : base( o )
            {
            }

            /// <summary>
            /// Gets the device if it exists in the host.
            /// </summary>
            public new TDevice? Device => (TDevice?)base.Device;

            /// <summary>
            /// Gets the Sidekick that manages this bridge.
            /// </summary>
            public TSidekick Sidekick => (TSidekick)_sidekick;

        }
    }

}
