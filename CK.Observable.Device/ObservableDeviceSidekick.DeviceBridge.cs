using CK.Core;
using CK.DeviceModel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable.Device
{
    public abstract partial class ObservableDeviceSidekick<THost,TObjectDevice,TObjectDeviceHost> where THost : IDeviceHost
        where TObjectDevice : ObservableObjectDevice<THost>
        where TObjectDeviceHost: ObservableObjectDeviceHost<THost>
    {

        /// <summary>
        /// Must create a <see cref="Bridge{TSidekick, TDevice}"/> between <typeparamref name="TObjectDevice"/> and its actual <see cref="Bridge{TSidekick, TDevice}.Device"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The bridge for the observable device object.</returns>
        protected abstract Bridge CreateBridge( IActivityMonitor monitor, TObjectDevice o );

        /// <summary>
        /// Non generic bridge between observable <see cref="TObjectDevice"/> and its actual <see cref="Device"/>.
        /// This cannot be directly instantiated: use the generic <see cref="Bridge{TSidekick,TDevice}"/> adapter.
        /// </summary>
        protected abstract class Bridge
        {
            internal ObservableDeviceSidekick<THost,TObjectDevice,TObjectDeviceHost> _sidekick;
            internal Bridge? _nextUnbound;

            /// <summary>
            /// Gets the observable object device.
            /// </summary>
            internal protected TObjectDevice Object { get; }

            /// <summary>
            /// Gets the associated device or null if no actual device with the <see cref="TObjectDevice.DeviceName"/> exists in the host.
            /// </summary>
            internal protected IDevice? Device { get; private set; }

            /// <summary>
            /// This is private protected so that developers are obliged to use the <see cref="Bridge{TDevice}"/> generic adapter.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            private protected Bridge( TObjectDevice o )
            {
                Debug.Assert( !o.IsDisposed );
                Object = o;
            }

            internal void Initialize( IActivityMonitor monitor, ObservableDeviceSidekick<THost, TObjectDevice, TObjectDeviceHost> owner, IDevice? initialDevice )
            {
                _sidekick = owner;
                if( initialDevice == null ) owner.AddUnbound( this );
                else
                {
                    Device = initialDevice;
                    OnDeviceAppeared( monitor );
                }
            }

            internal void SetDevice( IActivityMonitor monitor, IDevice d )
            {
                Debug.Assert( Device == null, "This is called only if the current Device is null." );
                Device = d;
                Object.Status = d.Status;
                Object.ConfigurationStatus = d.ConfigurationStatus;
                Object.ControllerKey = d.ControllerKey;
                d.StatusChanged.Async += OnDeviceStatusChanged;
                d.ControllerKeyChanged.Async += OnDeviceControllerKeyChanged;
                _sidekick.RemoveUnbound( this );
                OnDeviceAppeared( monitor );
            }

            internal void DetachDevice( IActivityMonitor monitor )
            {
                Debug.Assert( Device != null, "This is called only if a Device is bound." );
                Object.Status = null;
                Object.ConfigurationStatus = null;
                Object.ControllerKey = null;
                Device.StatusChanged.Async -= OnDeviceStatusChanged;
                Device.ControllerKeyChanged.Async -= OnDeviceControllerKeyChanged;
                _sidekick.AddUnbound( this );
                Device = null;
                OnDeviceDisappeared( monitor );
            }

            Task OnDeviceControllerKeyChanged( IActivityMonitor monitor, IDevice sender, string? controllerKey )
            {
                return ModifyAsync( monitor, () =>
                {
                    Object.ControllerKey = controllerKey;
                } );
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
                        Object.ControllerKey = sender.ControllerKey;
                    }
                } );
            }

            internal void OnDestroy( IActivityMonitor monitor, bool isObjectDeviceDisposed )
            {
                if( Device == null ) _sidekick.RemoveUnbound( this );
                else
                {
                    Device.StatusChanged.Async -= OnDeviceStatusChanged;
                    Device.ControllerKeyChanged.Async -= OnDeviceControllerKeyChanged;
                }
                if( isObjectDeviceDisposed )
                {
                    OnObjectDeviceDisposed( monitor );
                }
            }

            /// <inheritdoc cref="ObservableDomain.Modify(IActivityMonitor, Action, int)" />
            protected Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 ) => _sidekick.Domain.ModifyAsync( monitor, actions, millisecondsTimeout );

            /// <inheritdoc cref="ObservableDomain.ModifyNoThrowAsync(IActivityMonitor, Action, int)"/>.
            protected Task<(TransactionResult, Exception)> ModifyNoThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 ) => _sidekick.Domain.ModifyNoThrowAsync( monitor, actions, millisecondsTimeout );

            /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action, int)"/>.
            protected Task ModifyThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 ) => _sidekick.Domain.ModifyThrowAsync( monitor, actions, millisecondsTimeout );

            /// <summary>
            /// Called whenever the <see cref="Device"/> becames not null.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected abstract void OnDeviceAppeared( IActivityMonitor monitor );

            /// <summary>
            /// Called whenever the <see cref="Device"/> is no more available in the host.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected abstract void OnDeviceDisappeared( IActivityMonitor monitor );

            /// <summary>
            /// Called whenever the <see cref="ObservableObjectDevice{THost}"/> is disposed.
            /// Note that the <see cref="Device"/> may still exist in the host.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected virtual void OnObjectDeviceDisposed( IActivityMonitor monitor )
            {
            }
        }

        /// <summary>
        /// Base class to implement to bridge <typeparamref name="TDevice"/> to observable objects.
        /// </summary>
        /// <typeparam name="TSidekick">The type of the sidekick that manages this bridge.</typeparam>
        /// <typeparam name="TDevice">The type of the actual device.</typeparam>
        protected abstract class Bridge<TSidekick,TDevice> : Bridge
            where TSidekick : ObservableDeviceSidekick<THost, TObjectDevice, TObjectDeviceHost>
            where TDevice : class, IDevice
        {
            /// <summary>
            /// Initializes a new bridge.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            protected Bridge( TObjectDevice o )
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
