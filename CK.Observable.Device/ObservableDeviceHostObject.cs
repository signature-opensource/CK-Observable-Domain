using CK.DeviceModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Observable.Device
{
    /// <summary>
    /// Non generic abstract base class for device that is not intended to be specialized directly.
    /// Use the generic <see cref="ObservableDeviceHostObject{TSidekick}"/> as the object device base.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceHostObject : ObservableObject, ISidekickLocator
    {
        /// <summary>
        /// This is exposed as a typed TSidekick by ObservableDeviceHostObject{TSidekick}.
        /// </summary>
        private protected ObservableDomainSidekick _sidekick;

        ObservableDomainSidekick ISidekickLocator.Sidekick => _sidekick;

        /// <summary>
        /// Contains the set of devices' name.
        /// This set is mutable by specialization but this should be used this care: the actual devices
        /// are handled by the <see cref="IDeviceHost"/> (this property is not serialized).
        /// See <see cref="OnDevicesChanged"/> that merges the devices from the external device host (and can be overridden if needed).
        /// </summary>
        internal protected readonly ObservableSet<string> InternalDevices;

#pragma warning disable CS8618 // _sidekick is initialized by the first call to ApplyDevicesChanged.
        private protected ObservableDeviceHostObject()
        {
            InternalDevices = new ObservableSet<string>();
        }

        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected ObservableDeviceHostObject( RevertSerialization _ ) : base( _ ) { }

        ObservableDeviceHostObject( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            InternalDevices = new ObservableSet<string>();
        }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        void Write( BinarySerializer w )
        {
        }

        /// <summary>
        /// Gets an observable list of devices that are managed by the device host.
        /// This list is under control of the <see cref="IDeviceHost"/>.
        /// </summary>
        public IObservableReadOnlySet<string> Devices => InternalDevices;

        internal void ApplyDevicesChanged( ObservableDomainSidekick sidekick, IReadOnlyDictionary<string, IDevice> devices )
        {
            Debug.Assert( _sidekick == null || _sidekick == sidekick, "Initial call or just an update." );

            _sidekick = sidekick;
            OnDevicesChanged( devices );
        }

        /// <summary>
        /// Called during initialization of this host and each time the external devices changed.
        /// This method synchronizes the <see cref="Devices"/> collection.
        /// </summary>
        /// <param name="snapshot">The devices snapshot.</param>
        private protected virtual void OnDevicesChanged( IReadOnlyDictionary<string, IDevice> snapshot )
        {
            List<string>? toRemove = null;
            foreach( var here in InternalDevices )
            {
                if( !snapshot.ContainsKey( here ) )
                {
                    if( toRemove == null ) toRemove = new List<string>();
                    toRemove.Add( here );
                }
            }
            if( toRemove != null )
            {
                foreach( var noMore in toRemove )
                {
                    InternalDevices.Remove( noMore );
                }
            }
            InternalDevices.AddRange( snapshot.Keys );
        }

        protected override void OnDestroy()
        {
            InternalDevices.Destroy();
            // Using nullable just in case EnsureDomainSidekick has not been called.
            ((IInternalObservableDeviceSidekick?)_sidekick)?.OnObjectHostDestroyed( Domain.Monitor );
            base.OnDestroy();
        }


    }
}
