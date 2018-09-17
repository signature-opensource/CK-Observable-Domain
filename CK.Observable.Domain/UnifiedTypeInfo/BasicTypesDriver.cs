using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class BasicTypeDrivers
    {
        public sealed class DBool : UnifiedTypeDriverBase<Boolean>
        {
            public override bool ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadBoolean();
            }

            public override void WriteData( BinarySerializer w, bool o )
            {
                w.Write( o );
            }
            public override void Export( bool o, int num, ObjectExporter exporter ) => exporter.Target.EmitBool( o );
        }

        public sealed class DChar : UnifiedTypeDriverBase<Char>
        {
            public override char ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadChar();
            }

            public override void WriteData( BinarySerializer w, char o )
            {
                w.Write( o );
            }

            public override void Export( char o, int num, ObjectExporter exporter ) => exporter.Target.EmitChar( o );
        }

        public sealed class DDouble : UnifiedTypeDriverBase<Double>
        {
            public override double ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadDouble();
            }

            public override void WriteData( BinarySerializer w, double o )
            {
                w.Write( o );
            }

            public override void Export( double o, int num, ObjectExporter exporter ) => exporter.Target.EmitDouble( o );

        }

        public sealed class DSingle : UnifiedTypeDriverBase<Single>
        {
            public override float ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadSingle();
            }

            public override void WriteData( BinarySerializer w, float o )
            {
                w.Write( o );
            }
            public override void Export( float o, int num, ObjectExporter exporter ) => exporter.Target.EmitSingle( o );
        }

        public sealed class DDecimal : UnifiedTypeDriverBase<Decimal>
        {
            public override decimal ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadDecimal();
            }

            public override void WriteData( BinarySerializer w, decimal o )
            {
                w.Write( o );
            }

            public override void Export( decimal o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public sealed class DSByte : UnifiedTypeDriverBase<SByte>
        {
            public override sbyte ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadSByte();
            }

            public override void WriteData( BinarySerializer w, sbyte o )
            {
                w.Write( o );
            }
            public override void Export( sbyte o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public sealed class DByte : UnifiedTypeDriverBase<Byte>
        {
            public override byte ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadByte();
            }

            public override void WriteData( BinarySerializer w, byte o )
            {
                w.Write( o );
            }

            public override void Export( byte o, int num, ObjectExporter exporter ) => exporter.Target.EmitByte( o );
        }

        public sealed class DInt16 : UnifiedTypeDriverBase<Int16>
        {
            public override short ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadInt16();
            }

            public override void WriteData( BinarySerializer w, short o )
            {
                w.Write( o );
            }

            public override void Export( short o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt16( o );
        }

        public sealed class DUInt16 : UnifiedTypeDriverBase<UInt16>
        {
            public override ushort ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadUInt16();
            }

            public override void WriteData( BinarySerializer w, ushort o )
            {
                w.Write( o );
            }

            public override void Export( ushort o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt16( o );
        }

        public sealed class DInt32 : UnifiedTypeDriverBase<Int32>
        {
            public override int ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadInt32();
            }

            public override void WriteData( BinarySerializer w, int o )
            {
                w.Write( o );
            }

            public override void Export( int o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt32( o );
        }

        public sealed class DUInt32 : UnifiedTypeDriverBase<UInt32>
        {
            public override uint ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadUInt32();
            }

            public override void WriteData( BinarySerializer w, uint o )
            {
                w.Write( o );
            }

            public override void Export( uint o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt32( o );
        }

        public sealed class DInt64 : UnifiedTypeDriverBase<Int64>
        {
            public override long ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadInt64();
            }

            public override void WriteData( BinarySerializer w, long o )
            {
                w.Write( o );
            }

            public override void Export( long o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt64( o );
        }

        public sealed class DUInt64 : UnifiedTypeDriverBase<UInt64>
        {
            public override ulong ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadUInt64();
            }

            public override void WriteData( BinarySerializer w, ulong o )
            {
                w.Write( o );
            }

            public override void Export( ulong o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt64( o );
        }

        public sealed class DString : UnifiedTypeDriverBase<String>
        {
            public override string ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadNullableString();
            }

            public override void WriteData( BinarySerializer w, string o )
            {
                w.WriteNullableString( o );
            }

            public override void Export( string o, int num, ObjectExporter exporter ) => exporter.Target.EmitString( o );
        }

        public sealed class DGuid : UnifiedTypeDriverBase<Guid>
        {
            public override Guid ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadGuid();
            }

            public override void WriteData( BinarySerializer w, Guid o )
            {
                w.Write( o );
            }

            public override void Export( Guid o, int num, ObjectExporter exporter ) => exporter.Target.EmitGuid( o );
        }

        public sealed class DDateTime : UnifiedTypeDriverBase<DateTime>
        {
            public override DateTime ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadDateTime();
            }

            public override void WriteData( BinarySerializer w, DateTime o )
            {
                w.Write( o );
            }

            public override void Export( DateTime o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTime( o );
        }

        public sealed class DTimeSpan : UnifiedTypeDriverBase<TimeSpan>
        {
            public override TimeSpan ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadTimeSpan();
            }

            public override void WriteData( BinarySerializer w, TimeSpan o )
            {
                w.Write( o );
            }

            public override void Export( TimeSpan o, int num, ObjectExporter exporter ) => exporter.Target.EmitTimeSpan( o );
        }

        public sealed class DDateTimeOffset : UnifiedTypeDriverBase<DateTimeOffset>
        {
            public override DateTimeOffset ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                return r.ReadDateTimeOffset();
            }

            public override void WriteData( BinarySerializer w, DateTimeOffset o )
            {
                w.Write( o );
            }

            public override void Export( DateTimeOffset o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTimeOffset( o );
        }


    }
}
