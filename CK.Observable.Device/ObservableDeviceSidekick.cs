using CK.Core;
using CK.DeviceModel;
using CK.Observable;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for a sidekick that interfaces a <see cref="IDeviceHost"/>.
    /// </summary>
    /// <typeparam name="THost">Type of the device host.</typeparam>
    /// <typeparam name="TDeviceObject">Type of the observable object device.</typeparam>
    /// <typeparam name="TDeviceHostObject">Type of the observable device host.</typeparam>
    public abstract partial class ObservableDeviceSidekick<THost,TDeviceObject,TDeviceHostObject> : ObservableDomainSidekick, IInternalObservableDeviceSidekick
        where THost : IDeviceHost
        where TDeviceObject : ObservableDeviceObject
        where TDeviceHostObject: ObservableDeviceHostObject
    {
        // A Bridge exists if and only if the ObservableDeviceObject exists.
        // Actual devices that have no corresponding ObservableDeviceObject don't appear here.
        readonly Dictionary<string, DeviceBridge> _bridges;
        TDeviceHostObject? _objectHost;
        bool _deviceTracking;

        /// <summary>
        /// Initializes a new <see cref="ObservableDeviceSidekick{THost, TObjectDevice, TObjectDeviceHost}"/>.
        /// </summary>
        /// <param name="manager">The domain's sidekick manager.</param>
        /// <param name="host">The device host.</param>
        protected ObservableDeviceSidekick( IObservableDomainSidekickManager manager, THost host )
            : base( manager )
        {
            Host = host;
            _bridges = new Dictionary<string, DeviceBridge>();
        }

        /// <summary>
        /// Gets the device host (Device model).
        /// </summary>
        protected THost Host { get; }

        IDeviceHost IObservableDeviceSidekick.Host => Host;

        /// <summary>
        /// Gets the object device host if the observable object has been instantiated, null otherwise.
        /// </summary>
        protected TDeviceHostObject? ObjectHost => _objectHost;

        ObservableDeviceHostObject? IObservableDeviceSidekick.ObjectHost => _objectHost;

        public TDeviceObject? FindObservableDeviceObject( string deviceName ) => _bridges.GetValueOrDefault( deviceName )?.Object;

        ObservableDeviceObject? IObservableDeviceSidekick.FindObservableDeviceObject( string deviceName ) => FindObservableDeviceObject( deviceName );

        /// <summary>
        /// Gets the bridges: all the <typeparamref name="TDeviceObject"/> that exist in the domain
        /// (they may not be bound to their respective device: their <see cref="DeviceBridge.Device"/> can be null).
        /// </summary>
        protected IReadOnlyDictionary<string, DeviceBridge> Bridges => _bridges;

        /// <summary>
        /// Registers <typeparamref name="TDeviceObject"/> and <typeparamref name="TDeviceHostObject"/> instances.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The object that just appeared.</param>
        protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
        {
            // Note: We don't subscribe to the Destroyed event of the ObservableDeviceObject or ObservableDeviceHostObject:
            // their OnDestroy directly call our internal OnObject(Host)Destroy methods.
            if( o is TDeviceObject device )
            {
                if( _bridges.TryGetValue( device.DeviceName, out var bridge ) )
                {
                    throw new Exception( $"Duplicate device error: A device named '{device.DeviceName}' already exists in the domain (index {bridge.Object.OId.Index})." );
                }
                if( !_deviceTracking )
                {
                    Host.AllDevicesLifetimeEvent.Async += OnAllDevicesLifetimeEventAsync;
                    _deviceTracking = true;
                }
                bridge = CreateBridge( monitor, device );
                _bridges.Add( device.DeviceName, bridge );
                var d = Host.Find( device.DeviceName );
                bridge.Initialize( monitor, this, d );
                _objectHost?.Add( device, d );
            }
            else if( o is TDeviceHostObject host )
            {
                if( _objectHost != null )
                {
                    throw new Exception( $"There must be at most one device host object in a ObservableDomain. Object at index {_objectHost.OId.Index} is already registered." );
                }
                if( !_deviceTracking )
                {
                    Host.AllDevicesLifetimeEvent.Async += OnAllDevicesLifetimeEventAsync;
                    _deviceTracking = true;
                }
                _objectHost = host;
                var existingObjects = _bridges.Values.Select( b => b.Object );

                host.Initialize( this, Host.GetDevices(), existingObjects );
                OnObjectHostAppeared( monitor );
            }
        }

        Task OnAllDevicesLifetimeEventAsync( IActivityMonitor monitor, IDeviceHost sender, DeviceLifetimeEvent e )
        {
            return Domain.TryModifyAsync( monitor, () =>
            {
                var bridge = _bridges.GetValueOrDefault( e.Device.Name );
                // No bridge (no observable object) and no observable host: this sidekick is "useless", it is not
                // concerned by the device since no observables are interested.
                if( bridge == null && _objectHost == null ) return;

                if( e.Device.IsDestroyed )
                {
                    if( bridge?.Device != null ) bridge.DetachDevice( monitor );
                }
                else
                {
                    if( bridge != null )
                    {
                        if( bridge.Device == null )
                        {
                            bridge.SetDevice( monitor, e.Device );
                        }
                        else
                        {
                            bridge.UpdateDevice( monitor, e.Device );
                        }
                    }
                }
                // Updates the ODeviceInfo after the Object if it exists.
                _objectHost?.OnDeviceLifetimeEvent( monitor, e, bridge?.Object );
            } );
        }

        void IInternalObservableDeviceSidekick.OnObjectHostDestroyed( IActivityMonitor monitor )
        {
            _objectHost = null;
            OnObjectHostDisappeared( monitor );
        }

        void IInternalObservableDeviceSidekick.OnObjectDestroyed( IActivityMonitor monitor, ObservableDeviceObject o )
        {
            _bridges.Remove( o.DeviceName, out var bridge );
            Debug.Assert( bridge != null );
            bridge.OnDispose( monitor, isObjectDestroyed: true );
            _objectHost?.Remove( o );
        }

        /// <summary>
        /// Handles the command if it is a <see cref="BaseDeviceCommand"/> that the <see cref="Host"/> agrees to
        /// send it (see <see cref="IDeviceHost.SendCommand(IActivityMonitor, BaseDeviceCommand, bool, System.Threading.CancellationToken)"/>.
        /// </summary>
        /// <remarks>
        /// There is few reason to override this method but it could be done if needed.
        /// </remarks>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The sidekick command to handle.</param>
        /// <returns>True if this device sidekick handles the command, false otherwise.</returns>
        protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
        {
            if( command.Command is ObservableDeviceObject.ForceSendCommand force )
            {
                return SendDeviceCommand( monitor, force.SetControllerKeyCommand, false );
            }

            if( command.Command is BaseDeviceCommand c )
            {
                if( c is BaseConfigureDeviceCommand config )
                {
                    command.DomainPostActions.Add( ctx => Host.EnsureDeviceAsync( ctx.Monitor, config.Configuration ) );
                    return true;
                }
                return SendDeviceCommand( monitor, c, true );
            }
            return false;
        }

        bool SendDeviceCommand( IActivityMonitor monitor, BaseDeviceCommand c, bool checkControllerKey )
        {
            var result = Host.SendCommand( monitor, c, checkControllerKey );
            if( result != DeviceHostCommandResult.Success )
            {
                if( result == DeviceHostCommandResult.InvalidHostType )
                {
                    // This is not a command for this Host.
                    return false;
                }
                monitor.Warn( $"Command '{c}' has not been sent: {result}." );
            }
            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// This is sealed and calls the protected virtual <see cref="OnDispose(IActivityMonitor)"/> that
        /// can be overridden.
        /// </remarks>
        protected sealed override void OnUnload( IActivityMonitor monitor )
        {
            if( _deviceTracking )
            {
                Host.AllDevicesLifetimeEvent.Async -= OnAllDevicesLifetimeEventAsync;
            }
            foreach( var b in _bridges.Values )
            {
                b.OnDispose( monitor, isObjectDestroyed: false );
            }
            _bridges.Clear();
            _objectHost = null;
            OnDispose( monitor );
        }

        /// <summary>
        /// Optional extension point called when the <see cref="Host"/> object exists.
        /// This default implementation does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        protected virtual void OnObjectHostAppeared( IActivityMonitor monitor )
        {
        }

        /// <summary>
        /// Optional extension point called when the <see cref="Host"/> object is disposed.
        /// This default implementation does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        protected virtual void OnObjectHostDisappeared( IActivityMonitor monitor )
        {
        }

        /// <summary>
        /// Called when the domain is unloaded or destroyed.
        /// All the bridges have already been disposed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        protected virtual void OnDispose( IActivityMonitor monitor )
        {
        }
    }

}
