using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class BasicTypeDrivers
    {
        public class DBool : UnifiedTypeDriverBase<Boolean>
        {
            public override bool ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadBoolean();
            }

            public override void WriteData( BinarySerializer w, bool o )
            {
                w.Write( o );
            }
            public override void Export( bool o, int num, ObjectExporter exporter ) => exporter.Target.EmitBool( o );
        }

        public class DChar : UnifiedTypeDriverBase<Char>
        {
            public override char ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadChar();
            }

            public override void WriteData( BinarySerializer w, char o )
            {
                w.Write( o );
            }

            public override void Export( char o, int num, ObjectExporter exporter ) => exporter.Target.EmitChar( o );
        }

        public class DDouble : UnifiedTypeDriverBase<Double>
        {
            public override double ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadDouble();
            }

            public override void WriteData( BinarySerializer w, double o )
            {
                w.Write( o );
            }

            public override void Export( double o, int num, ObjectExporter exporter ) => exporter.Target.EmitDouble( o );

        }

        public class DSingle : UnifiedTypeDriverBase<Single>
        {
            public override float ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadSingle();
            }

            public override void WriteData( BinarySerializer w, float o )
            {
                w.Write( o );
            }
            public override void Export( float o, int num, ObjectExporter exporter ) => exporter.Target.EmitSingle( o );
        }

        public class DDecimal : UnifiedTypeDriverBase<Decimal>
        {
            public override decimal ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadDecimal();
            }

            public override void WriteData( BinarySerializer w, decimal o )
            {
                w.Write( o );
            }

            public override void Export( decimal o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public class DSByte : UnifiedTypeDriverBase<SByte>
        {
            public override sbyte ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadSByte();
            }

            public override void WriteData( BinarySerializer w, sbyte o )
            {
                w.Write( o );
            }
            public override void Export( sbyte o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public class DByte : UnifiedTypeDriverBase<Byte>
        {
            public override byte ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadByte();
            }

            public override void WriteData( BinarySerializer w, byte o )
            {
                w.Write( o );
            }

            public override void Export( byte o, int num, ObjectExporter exporter ) => exporter.Target.EmitByte( o );
        }

        public class DInt16 : UnifiedTypeDriverBase<Int16>
        {
            public override short ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadInt16();
            }

            public override void WriteData( BinarySerializer w, short o )
            {
                w.Write( o );
            }

            public override void Export( short o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt16( o );
        }

        public class DUInt16 : UnifiedTypeDriverBase<UInt16>
        {
            public override ushort ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadUInt16();
            }

            public override void WriteData( BinarySerializer w, ushort o )
            {
                w.Write( o );
            }

            public override void Export( ushort o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt16( o );
        }

        public class DInt32 : UnifiedTypeDriverBase<Int32>
        {
            public override int ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadInt32();
            }

            public override void WriteData( BinarySerializer w, int o )
            {
                w.Write( o );
            }

            public override void Export( int o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt32( o );
        }

        public class DUInt32 : UnifiedTypeDriverBase<UInt32>
        {
            public override uint ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadUInt32();
            }

            public override void WriteData( BinarySerializer w, uint o )
            {
                w.Write( o );
            }

            public override void Export( uint o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt32( o );
        }

        public class DInt64 : UnifiedTypeDriverBase<Int64>
        {
            public override long ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadInt64();
            }

            public override void WriteData( BinarySerializer w, long o )
            {
                w.Write( o );
            }

            public override void Export( long o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt64( o );
        }

        public class DUInt64 : UnifiedTypeDriverBase<UInt64>
        {
            public override ulong ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadUInt64();
            }

            public override void WriteData( BinarySerializer w, ulong o )
            {
                w.Write( o );
            }

            public override void Export( ulong o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt64( o );
        }

        public class DString : UnifiedTypeDriverBase<String>
        {
            public override string ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadNullableString();
            }

            public override void WriteData( BinarySerializer w, string o )
            {
                w.WriteNullableString( o );
            }

            public override void Export( string o, int num, ObjectExporter exporter ) => exporter.Target.EmitString( o );
        }

        public class DGuid : UnifiedTypeDriverBase<Guid>
        {
            public override Guid ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadGuid();
            }

            public override void WriteData( BinarySerializer w, Guid o )
            {
                w.Write( o );
            }

            public override void Export( Guid o, int num, ObjectExporter exporter ) => exporter.Target.EmitGuid( o );
        }

        public class DDateTime : UnifiedTypeDriverBase<DateTime>
        {
            public override DateTime ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadDateTime();
            }

            public override void WriteData( BinarySerializer w, DateTime o )
            {
                w.Write( o );
            }

            public override void Export( DateTime o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTime( o );
        }

        public class DTimeSpan : UnifiedTypeDriverBase<TimeSpan>
        {
            public override TimeSpan ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadTimeSpan();
            }

            public override void WriteData( BinarySerializer w, TimeSpan o )
            {
                w.Write( o );
            }

            public override void Export( TimeSpan o, int num, ObjectExporter exporter ) => exporter.Target.EmitTimeSpan( o );
        }

        public class DDateTimeOffset : UnifiedTypeDriverBase<DateTimeOffset>
        {
            public override DateTimeOffset ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                return r.Reader.ReadDateTimeOffset();
            }

            public override void WriteData( BinarySerializer w, DateTimeOffset o )
            {
                w.Write( o );
            }

            public override void Export( DateTimeOffset o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTimeOffset( o );
        }


    }
}
