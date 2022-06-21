using CK.BinarySerialization;
using CK.Core;
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
        private protected IObservableDeviceSidekick _sidekick;

        ObservableDomainSidekick ISidekickLocator.Sidekick => (ObservableDomainSidekick)_sidekick;

#pragma warning disable CS8618 // _sidekick is initialized by the first call to ApplyDevicesChanged.
        private protected ObservableDeviceHostObject()
        {
        }


        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected ObservableDeviceHostObject( Sliced _ ) : base( _ ) { }


        ObservableDeviceHostObject( IBinaryDeserializer d, ITypeReadInfo info )
                : base( Sliced.Instance )
        {
        }

#pragma warning restore CS8618 // _sidekick is initialized by the first call to ApplyDevicesChanged.

        public static void Write( IBinarySerializer w, in ObservableDeviceHostObject o )
        {
        }

        internal void Initialize( IObservableDeviceSidekick sidekick,
                                  IReadOnlyDictionary<string, IDevice> devices,
                                  IEnumerable<ObservableDeviceObject> existingObjects )
        {
            Debug.Assert( _sidekick == null && sidekick != null);
            _sidekick = sidekick;
            Initialize( devices, existingObjects );
        }

        internal abstract IEnumerable<string> GetAvailableDeviceNames();

        private protected abstract void Initialize( IReadOnlyDictionary<string, IDevice> devices, IEnumerable<ObservableDeviceObject> existingObjects );

        internal abstract void OnDeviceLifetimeEvent( IActivityMonitor monitor, DeviceLifetimeEvent e, ObservableDeviceObject? o );

        internal abstract void Add( ObservableDeviceObject o, IDevice? d );

        internal abstract void Remove( ObservableDeviceObject o );

    }
}
