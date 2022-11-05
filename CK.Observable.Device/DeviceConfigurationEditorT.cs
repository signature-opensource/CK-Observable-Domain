using CK.BinarySerialization;
using CK.Core;
using CK.Observable;
using CK.Observable.Device;
using CK.DeviceModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System;
using Microsoft.Extensions.Hosting;

namespace CK.Observable.Device
{
    [SerializationVersion( 0 )]
    public class DeviceConfigurationEditor<TConfig> : DeviceConfigurationEditor
        where TConfig : DeviceConfiguration, new()
    {

        internal protected DeviceConfigurationEditor( ObservableDeviceObject owner)
            : base(owner)
        {
            if(base.Local == null )
            {
                _local = new TConfig();
            }
        }

        DeviceConfigurationEditor( IBinaryDeserializer r,ITypeReadInfo info ) : base( r, info ) 
        {
        }

        public static void Write( IBinarySerializer s, in DeviceConfigurationEditor<TConfig> o )
        {
        }

        public new TConfig Local => (TConfig)base.Local;
    }

}
