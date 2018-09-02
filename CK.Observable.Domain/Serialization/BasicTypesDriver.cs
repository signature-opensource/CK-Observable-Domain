using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class BasicTypeDrivers
    {
        public class DBool : ExternalTypeSerializationDriver<Boolean>
        {
            public override bool ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadBoolean();
            }

            public override void WriteData( Serializer w, bool o )
            {
                w.Write( o );
            }
        }

        public class DChar : ExternalTypeSerializationDriver<Char>
        {
            public override char ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadChar();
            }

            public override void WriteData( Serializer w, char o )
            {
                w.Write( o );
            }
        }

        public class DDouble : ExternalTypeSerializationDriver<Double>
        {
            public override double ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadDouble();
            }

            public override void WriteData( Serializer w, double o )
            {
                w.Write( o );
            }
        }

        public class DSingle : ExternalTypeSerializationDriver<Single>
        {
            public override float ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadSingle();
            }

            public override void WriteData( Serializer w, float o )
            {
                w.Write( o );
            }
        }

        public class DDecimal : ExternalTypeSerializationDriver<Decimal>
        {
            public override decimal ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadDecimal();
            }

            public override void WriteData( Serializer w, decimal o )
            {
                w.Write( o );
            }
        }

        public class DSByte : ExternalTypeSerializationDriver<SByte>
        {
            public override sbyte ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadSByte();
            }

            public override void WriteData( Serializer w, sbyte o )
            {
                w.Write( o );
            }
        }

        public class DByte : ExternalTypeSerializationDriver<Byte>
        {
            public override byte ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadByte();
            }

            public override void WriteData( Serializer w, byte o )
            {
                w.Write( o );
            }
        }

        public class DInt16 : ExternalTypeSerializationDriver<Int16>
        {
            public override short ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadInt16();
            }

            public override void WriteData( Serializer w, short o )
            {
                w.Write( o );
            }
        }

        public class DUInt16 : ExternalTypeSerializationDriver<UInt16>
        {
            public override ushort ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadUInt16();
            }

            public override void WriteData( Serializer w, ushort o )
            {
                w.Write( o );
            }
        }

        public class DInt32 : ExternalTypeSerializationDriver<Int32>
        {
            public override int ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadInt32();
            }

            public override void WriteData( Serializer w, int o )
            {
                w.Write( o );
            }
        }

        public class DUInt32 : ExternalTypeSerializationDriver<UInt32>
        {
            public override uint ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadUInt32();
            }

            public override void WriteData( Serializer w, uint o )
            {
                w.Write( o );
            }
        }

        public class DInt64 : ExternalTypeSerializationDriver<Int64>
        {
            public override long ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadInt64();
            }

            public override void WriteData( Serializer w, long o )
            {
                w.Write( o );
            }
        }

        public class DUInt64 : ExternalTypeSerializationDriver<UInt64>
        {
            public override ulong ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadUInt64();
            }

            public override void WriteData( Serializer w, ulong o )
            {
                w.Write( o );
            }
        }

        public class DString : ExternalTypeSerializationDriver<String>
        {
            public override string ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return r.ReadNullableString();
            }

            public override void WriteData( Serializer w, string o )
            {
                w.WriteNullableString( o );
            }
        }

        public class DGuid : ExternalTypeSerializationDriver<Guid>
        {
            public override Guid ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return new Guid( r.ReadBytes( 16 ) );
            }

            public override void WriteData( Serializer w, Guid o )
            {
                w.Write( o.ToByteArray() );
            }
        }

        public class DDateTime : ExternalTypeSerializationDriver<DateTime>
        {
            public override DateTime ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return DateTime.FromBinary( r.ReadInt64() );
            }

            public override void WriteData( Serializer w, DateTime o )
            {
                w.Write( o.ToBinary() );
            }
        }

        public class DTimeSpan : ExternalTypeSerializationDriver<TimeSpan>
        {
            public override TimeSpan ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return TimeSpan.FromTicks( r.ReadInt64() );
            }

            public override void WriteData( Serializer w, TimeSpan o )
            {
                w.Write( o.Ticks );
            }
        }

        public class DDateTimeOffset : ExternalTypeSerializationDriver<DateTimeOffset>
        {
            public override DateTimeOffset ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo )
            {
                return new DateTimeOffset( r.ReadInt64(), TimeSpan.FromTicks( r.ReadInt64() ) );
            }

            public override void WriteData( Serializer w, DateTimeOffset o )
            {
                w.Write( o.DateTime.ToBinary() );
                w.Write( o.Offset.Ticks );
            }
        }


    }
}
