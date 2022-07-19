using CK.Core;
using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable.Device
{
    public abstract partial class ObservableDeviceSidekick<THost,TDeviceObject,TDeviceHostObject>
    {
        /// <summary>
        /// Must create a <see cref="PassiveBridge{TSidekick, TDevice}"/> between <typeparamref name="TDeviceObject"/> and its actual <see cref="PassiveBridge{TSidekick, TDevice}.Device"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The observable object to be bridged.</param>
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
        /// This cannot be directly instantiated: the generic <see cref="PassiveBridge{TSidekick,TDevice}"/>
        /// or <see cref="ActiveBridge{TSidekick, TDevice, TDeviceEvent}"/> adapter must be used.
        /// </summary>
        internal protected abstract class DeviceBridge : ObservableDeviceObject.IInternalDeviceBridge
        {
            internal ObservableDeviceSidekick<THost,TDeviceObject,TDeviceHostObject> _sidekick;

            /// <summary>
            /// Gets the observable object device.
            /// </summary>
            public TDeviceObject Object { get; }

            /// <summary>
            /// Gets the associated device or null if no actual device with the <see cref="ObservableDeviceObject.DeviceName"/> exists in the host.
            /// </summary>
            public IDevice? Device { get; private set; }

            #pragma warning disable CS8618 // Non-nullable _sidekick uninitialized. Consider declaring as nullable.
            /// <summary>
            /// This is private protected so that developers are obliged to use the <see cref="PassiveBridge{TSidekick,TDevice}"/> generic adapter.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            private protected DeviceBridge( TDeviceObject o )
            {
                Debug.Assert( !o.IsDestroyed );
                Object = o;
                o._bridge = this;
            }
            #pragma warning restore CS8618

            ObservableDomainSidekick ISidekickLocator.Sidekick => _sidekick;

            BaseStartDeviceCommand ObservableDeviceObject.IInternalDeviceBridge.CreateStartCommand() => new StartDeviceCommand<THost>();
            BaseConfigureDeviceCommand ObservableDeviceObject.IInternalDeviceBridge.CreateConfigureCommand( DeviceConfiguration? configuration ) => _sidekick.Host.CreateConfigureCommand( configuration );
            BaseStopDeviceCommand ObservableDeviceObject.IInternalDeviceBridge.CreateStopCommand() => new StopDeviceCommand<THost>();
            BaseDestroyDeviceCommand ObservableDeviceObject.IInternalDeviceBridge.CreateDestroyCommand() => new DestroyDeviceCommand<THost>();
            BaseSetControllerKeyDeviceCommand ObservableDeviceObject.IInternalDeviceBridge.CreateSetControllerKeyCommand() => new SetControllerKeyDeviceCommand<THost>();

            IEnumerable<string> ObservableDeviceObject.IInternalDeviceBridge.CurrentlyAvailableDeviceNames => _sidekick._objectHost?.GetAvailableDeviceNames()
                                                                                                                ?? _sidekick.Host.GetDevices().Keys;

            internal void Initialize( IActivityMonitor monitor, ObservableDeviceSidekick<THost, TDeviceObject, TDeviceHostObject> owner, IDevice? initialDevice )
            {
                _sidekick = owner;
                if( initialDevice != null ) SetDevice( monitor, initialDevice );
            }

            internal void SetDevice( IActivityMonitor monitor, IDevice d )
            {
                Debug.Assert( Device == null, "This is called only if the current Device is null." );
                Device = d;
                SubscribeDeviceEvent();

                Object._isRunning = d.Status.IsRunning;
                Object._configuration = d.ExternalConfiguration;
                Object._deviceControlStatus = ObservableDeviceObject.ComputeStatus( d, _sidekick.Domain.DomainName );
                OnDeviceAppeared( monitor );
                RaiseDevicePropertiesChanged( true, true, true, null );
            }

            internal void UpdateDevice( IActivityMonitor monitor, IDevice d )
            {
                Debug.Assert( Device == d, "This is called to update the current Device." );
                bool rC = Object._isRunning != d.Status.IsRunning;
                if( rC ) Object._isRunning = d.Status.IsRunning;
                var previous = Object._configuration;
                bool cC = previous != d.ExternalConfiguration;
                if( cC ) Object._configuration = d.ExternalConfiguration;
                var s = ObservableDeviceObject.ComputeStatus( d, _sidekick.Domain.DomainName );
                bool sC = Object._deviceControlStatus != s;
                if( sC ) Object._deviceControlStatus = s;
                if( rC || cC || sC )
                {
                    RaiseDevicePropertiesChanged( rC, cC, sC, previous );
                    OnDeviceChanged( monitor, rC, cC, sC, previous );
                }
            }

            internal void DetachDevice( IActivityMonitor monitor )
            {
                Debug.Assert( Device != null, "This is called only if a Device is bound." );
                UnsubscribeDeviceEvent();
                OnDeviceDisappearing( monitor );

                Device = null;
                Object._isRunning = null;
                var previous = Object._configuration;
                Object._configuration = null;
                Object._deviceControlStatus = DeviceControlStatus.MissingDevice;
                RaiseDevicePropertiesChanged( true, true, true, previous );
            }

            void RaiseDevicePropertiesChanged( bool runningChanged, bool configurationChanged, bool statusChanged, DeviceConfiguration? previous )
            {
                if( runningChanged ) Object.OnIsRunningChanged();
                if( configurationChanged ) Object.OnConfigurationChanged( previous );
                if( statusChanged ) Object.OnDeviceControlStatusChanged();
            }

            private protected virtual void SubscribeDeviceEvent()
            {
            }

            private protected virtual void UnsubscribeDeviceEvent()
            {
            }

            /// <summary>
            /// Called when this bridge must be disposed because the observable <see cref="Object"/>
            /// is unloaded or destroyed.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="isObjectDestroyed">True when the object has been destroyed, false when it is only unloaded.</param>
            internal void OnDispose( IActivityMonitor monitor, bool isObjectDestroyed )
            {
                if( Device != null ) UnsubscribeDeviceEvent();
                OnObjectDisappeared( monitor, isObjectDestroyed );
            }

            /// <inheritdoc cref="ObservableDeviceObject.CreateDeviceCommand{T}(Action{T}?)" />
            public T CreateCommand<T>( Action<T>? configuration ) where T : BaseDeviceCommand, new()
            {
                var c = new T();
                if( !c.HostType.IsAssignableFrom( _sidekick.Host.GetType() ) )
                {
                    Throw.InvalidOperationException( $"Command '{c.GetType().Name}' is bound to HostType '{c.HostType.Name}'. It cannot be handled by host {_sidekick.Host.GetType().Name}." );
                }
                c.DeviceName = Object.DeviceName;
                c.ControllerKey = Device?.ControllerKey;
                configuration?.Invoke( c );
                return c;
            }

            /// <summary>
            /// Called whenever the <see cref="Device"/> became not null.
            /// The <see cref="Object"/> (and any other objects of the domain) can be safely modified
            /// since the domain's write lock is held.
            /// <para>
            /// Does nothing at this level.
            /// </para>
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected virtual void OnDeviceAppeared( IActivityMonitor monitor )
            {
            }

            /// <summary>
            /// Called whenever a change occur in the bound <see cref="Device"/> that impacts
            /// <see cref="ObservableDeviceObject.IsRunning"/>, <see cref="ObservableDeviceObject.Configuration"/>
            /// or <see cref="ObservableDeviceObject.DeviceControlStatus"/>.
            /// The <see cref="Object"/> (and any other objects of the domain) can be safely modified
            /// since the domain's write lock is held.
            /// <para>
            /// Does nothing at this level.
            /// </para>
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="runningChanged">True if the <see cref="ObservableDeviceObject.IsRunning"/> has changed.</param>
            /// <param name="configurationChanged">True if the <see cref="ObservableDeviceObject.Configuration"/> has changed (<paramref name="previous"/> references the previous one).</param>
            /// <param name="statusChanged">True if the <see cref="ObservableDeviceObject.DeviceControlStatus"/> changed.</param>
            /// <param name="previous">The previous configuration if it has changed, the current one otherwise.</param>
            protected virtual void OnDeviceChanged( IActivityMonitor monitor,
                                                    bool runningChanged,
                                                    bool configurationChanged,
                                                    bool statusChanged,
                                                    DeviceConfiguration? previous )
            {
            }

            /// <summary>
            /// Called whenever the <see cref="Device"/> is no more available in the host: it is
            /// still not null here and events unregistering should be done.
            /// The observable <see cref="Object"/> (and any other objects of the domain) can
            /// be safely modified since the domain's write lock is held.
            /// <para>
            /// Does nothing at this level.
            /// </para>
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            protected virtual void OnDeviceDisappearing( IActivityMonitor monitor )
            {
            }

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
        /// Base class to implement to bridge <typeparamref name="TDevice"/> that are not <see cref="IActiveDevice"/>
        /// to observable objects.
        /// Specialized classes have access to the observable object (<see cref="DeviceBridge.Object"/>),
        /// the device that may not exist (<see cref="Device"/>) and the <see cref="Sidekick"/> in a
        /// strongly typed manner.
        /// </summary>
        /// <typeparam name="TSidekick">The type of the sidekick that manages this bridge.</typeparam>
        /// <typeparam name="TDevice">The type of the actual device.</typeparam>
        internal protected abstract class PassiveBridge<TSidekick, TDevice> : DeviceBridge
            where TSidekick : ObservableDeviceSidekick<THost, TDeviceObject, TDeviceHostObject>
            where TDevice : class, IDevice
        {
            /// <summary>
            /// Initializes a new bridge.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            protected PassiveBridge( TDeviceObject o )
                : base( o )
            {
            }

            /// <inheritdoc cref="PassiveBridge{TSidekick,TDevice}.Device" />
            public new TDevice? Device => (TDevice?)base.Device;

            /// <summary>
            /// Gets the Sidekick that manages this bridge.
            /// </summary>
            public TSidekick Sidekick => (TSidekick)_sidekick;
        }

        /// <summary>
        /// Base class to implement to bridge <typeparamref name="TDevice"/> that are <see cref="IActiveDevice"/>
        /// to observable objects.
        /// <para>
        /// Specialized classes have access to the observable object (<see cref="DeviceBridge.Object"/>),
        /// the device that may not exist (<see cref="Device"/>) and the <see cref="Sidekick"/> in a
        /// strongly typed manner.
        /// </para>
        /// <para>
        /// Since the device is active, the abstract <see cref="OnDeviceEventAsync"/> must be used to
        /// handle device events.
        /// </para>
        /// </summary>
        /// <typeparam name="TSidekick">The type of the sidekick that manages this bridge.</typeparam>
        /// <typeparam name="TDevice">The type of the actual device.</typeparam>
        /// <typeparam name="TDeviceEvent">The base type of the device event.</typeparam>
        internal protected abstract class ActiveBridge<TSidekick, TDevice, TDeviceEvent> : DeviceBridge
            where TSidekick : ObservableDeviceSidekick<THost, TDeviceObject, TDeviceHostObject>
            where TDevice : class, IActiveDevice<TDeviceEvent>
            where TDeviceEvent : ActiveDeviceEvent<TDevice>
        {
            /// <summary>
            /// Initializes a new bridge.
            /// </summary>
            /// <param name="o">The observable object device.</param>
            protected ActiveBridge( TDeviceObject o )
                : base( o )
            {
            }

            /// <inheritdoc cref="PassiveBridge{TSidekick,TDevice}.Device" />
            public new TDevice? Device => (TDevice?)base.Device;

            /// <summary>
            /// Gets the Sidekick that manages this bridge.
            /// </summary>
            public TSidekick Sidekick => (TSidekick)_sidekick;

            private protected override void SubscribeDeviceEvent()
            {
                Debug.Assert( Device != null );
                Device.DeviceEvent.Async += OnDeviceEventAsync;
            }

            private protected override void UnsubscribeDeviceEvent()
            {
                Debug.Assert( Device != null );
                Device.DeviceEvent.Async -= OnDeviceEventAsync;
            }

            /// <inheritdoc cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action?, bool, int, bool, bool, bool)" />
            protected Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                           Action actions,
                                                           bool throwException,
                                                           int millisecondsTimeout,
                                                           bool considerRolledbackAsFailure,
                                                           bool parallelDomainPostActions,
                                                           bool waitForDomainPostActionsCompletion )
            {
                return _sidekick.Domain.ModifyAsync( monitor,
                                                     actions,
                                                     throwException,
                                                     millisecondsTimeout,
                                                     considerRolledbackAsFailure,
                                                     parallelDomainPostActions,
                                                     waitForDomainPostActionsCompletion );
            }

            /// <inheritdoc cref="ObservableDomain.TryModifyAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>.
            protected Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                              Action actions,
                                                              int millisecondsTimeout = -1,
                                                              bool considerRolledbackAsFailure = true,
                                                              bool parallelDomainPostActions = true,
                                                              bool waitForDomainPostActionsCompletion = false )
            {
                return _sidekick.Domain.TryModifyAsync( monitor,
                                                        actions,
                                                        millisecondsTimeout,
                                                        considerRolledbackAsFailure,
                                                        parallelDomainPostActions,
                                                        waitForDomainPostActionsCompletion );
            }

            /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>.
            protected Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                                Action? actions,
                                                                int millisecondsTimeout = -1,
                                                                bool considerRolledbackAsFailure = true,
                                                                bool parallelDomainPostActions = true,
                                                                bool waitForDomainPostActionsCompletion = false )
            {
                return _sidekick.Domain.ModifyThrowAsync( monitor,
                                                          actions,
                                                          millisecondsTimeout,
                                                          considerRolledbackAsFailure,
                                                          parallelDomainPostActions,
                                                          waitForDomainPostActionsCompletion );
            }

            /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync{TResult}(IActivityMonitor, Func{TResult}, int, bool, bool)"/>.
            protected Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                               Func<TResult> actions,
                                                               int millisecondsTimeout = -1,
                                                               bool parallelDomainPostActions = true,
                                                               bool waitForDomainPostActionsCompletion = false )
            {
                return _sidekick.Domain.ModifyThrowAsync( monitor,
                                                          actions,
                                                          millisecondsTimeout,
                                                          parallelDomainPostActions,
                                                          waitForDomainPostActionsCompletion );
            }

            /// <inheritdoc cref="ObservableDomain.TryRead(IActivityMonitor, Action, int)"/>.
            protected bool TryRead( IActivityMonitor monitor, Action reader, int millisecondsTimeout )
            {
                return _sidekick.Domain.TryRead( monitor, reader, millisecondsTimeout );
            }

            /// <inheritdoc cref="ObservableDomain.TryRead{T}(IActivityMonitor, Func{T}, out T, int)"/>.
            protected bool TryRead<T>( IActivityMonitor monitor, Func<T> reader, [MaybeNullWhen( false )] out T result, int millisecondsTimeout )
            {
                return _sidekick.Domain.TryRead( monitor, reader, out result, millisecondsTimeout );
            }

            /// <summary>
            /// Can handle the device events.
            /// Note that lifetime events are handled separately and are reflected on the <see cref="ObservableDeviceObject"/>.
            /// <para>
            /// This is called by the device, either from the command loop (for <see cref="SimpleActiveDevice{TConfiguration, TEvent}"/>)
            /// or the event loop (for <see cref="ActiveDevice{TConfiguration, TEvent}"/>): unless appropriate synchronization is done,
            /// this executes concurrently with the domain state. <see cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action, int, bool)"/>
            /// must be used to interact with the domain.
            /// </para>
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="deviceEvent">The event to handle.</param>
            /// <returns>The awaitable.</returns>
            protected abstract Task OnDeviceEventAsync( IActivityMonitor monitor, TDeviceEvent deviceEvent );
        }
    }

}
