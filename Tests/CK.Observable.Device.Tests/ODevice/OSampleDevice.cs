using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    [SerializationVersion( 0 )]
    public class OSampleDevice : ObservableObjectDevice<SampleDeviceHost>
    {
        public OSampleDevice( string deviceName )
            : base( deviceName )
        {
        }

        public OSampleDevice( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            ctx.StartReading();
        }

        void Write( BinarySerializer w )
        {
        }

        public string Message { get; internal set; }

    }
}
