using CK.Core;
using CK.DeviceModel;

namespace CK.Observable.Device.Tests
{
    public class SampleDeviceConfiguration : DeviceConfiguration
    {
        public SampleDeviceConfiguration()
        {
            Message = "Hello!";
        }

        public SampleDeviceConfiguration( ICKBinaryReader r )
            : base( r )
        {
            r.ReadByte(); // version.
            PeriodMilliseconds = r.ReadInt32();
            Message = r.ReadString();
        }

        public override void Write( ICKBinaryWriter w )
        {
            base.Write( w );
            w.Write( (byte)0 );
            w.Write( PeriodMilliseconds );
            w.Write( Message );
        }

        public int PeriodMilliseconds { get; set; }

        public string Message { get; set; }
    }
}
