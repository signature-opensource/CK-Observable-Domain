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

            public override bool IsExportable => true;

            public override void Export( int num, bool o, ObjectExporter exporter ) => exporter.Target.EmitBool( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, char o, ObjectExporter exporter ) => exporter.Target.EmitChar( o );
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

            public override bool IsExportable => true;

            public override void Export( int num, double o, ObjectExporter exporter ) => exporter.Target.EmitDouble( o );

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
            public override bool IsExportable => true;

            public override void Export( int num, float o, ObjectExporter exporter ) => exporter.Target.EmitDouble( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, decimal o, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, sbyte o, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, byte o, ObjectExporter exporter ) => exporter.Target.EmitByte( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, short o, ObjectExporter exporter ) => exporter.Target.EmitInt16( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, ushort o, ObjectExporter exporter ) => exporter.Target.EmitUInt16( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, int o, ObjectExporter exporter ) => exporter.Target.EmitInt32( o );
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

            public override bool IsExportable => true;

            public override void Export( int num, uint o, ObjectExporter exporter ) => exporter.Target.EmitUInt32( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, long o, ObjectExporter exporter ) => exporter.Target.EmitInt64( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, ulong o, ObjectExporter exporter ) => exporter.Target.EmitUInt64( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, string o, ObjectExporter exporter ) => exporter.Target.EmitString( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, Guid o, ObjectExporter exporter ) => exporter.Target.EmitGuid( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, DateTime o, ObjectExporter exporter ) => exporter.Target.EmitDateTime( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, TimeSpan o, ObjectExporter exporter ) => exporter.Target.EmitTimeSpan( o );
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
            public override bool IsExportable => true;

            public override void Export( int num, DateTimeOffset o, ObjectExporter exporter ) => exporter.Target.EmitDateTimeOffset( o );
        }


    }
}
