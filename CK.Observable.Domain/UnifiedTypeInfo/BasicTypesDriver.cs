using System;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    public class BasicTypeDrivers
    {
        /// <summary>
        /// Handles object root type: redirects to standard object handling that knows how to deal
        /// with pure object.
        /// This driver is required for object[], List{object} or any other generic type that do not
        /// specify a more precise type.
        /// </summary>
        public sealed class DObject : UnifiedTypeDriverBase<Object>
        {
            public DObject() : base( isFinalType: false ) { }

            public static readonly DObject Default = new DObject();

            public override object ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) =>  r.ReadObject();

            public override void WriteData( BinarySerializer w, object o ) => w.WriteObject( o );

            public override void Export( object o, int num, ObjectExporter exporter ) => exporter.ExportObject( o );

        }

        public sealed class DType : UnifiedTypeDriverBase<Type>
        {
            public DType() { }

            public static readonly DType Default = new DType();

            public override Type ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => r.ReadType();

            public override void WriteData( BinarySerializer w, Type t ) => w.Write( t );

            public override void Export( Type o, int num, ObjectExporter exporter )
            {
                var t = exporter.Target;
                t.EmitStartObject( num, ObjectExportedKind.Object );
                t.EmitPropertyName( nameof( Type.AssemblyQualifiedName ) );
                t.EmitString( o.AssemblyQualifiedName );
                t.EmitEndObject( num, ObjectExportedKind.Object );
            }
        }

        public sealed class DBool : UnifiedTypeDriverBase<Boolean>
        {
            public DBool() { }

            public static readonly DBool Default = new DBool();

            public override bool ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => r.ReadBoolean();

            public override void WriteData( BinarySerializer w, bool o ) => w.Write( o );

            public override void Export( bool o, int num, ObjectExporter exporter ) => exporter.Target.EmitBool( o );
        }

        public sealed class DChar : UnifiedTypeDriverBase<Char>
        {
            public DChar() { }

            public static readonly DChar Default = new DChar();

            public override char ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadChar();
            
            public override void WriteData( BinarySerializer w, char o ) => w.Write( o );

            public override void Export( char o, int num, ObjectExporter exporter ) => exporter.Target.EmitChar( o );
        }

        public sealed class DDouble : UnifiedTypeDriverBase<Double>
        {
            public DDouble() { }

            public static readonly DDouble Default = new DDouble();

            public override double ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadDouble();

            public override void WriteData( BinarySerializer w, double o ) => w.Write( o );

            public override void Export( double o, int num, ObjectExporter exporter ) => exporter.Target.EmitDouble( o );
        }

        public sealed class DSingle : UnifiedTypeDriverBase<Single>
        {
            public DSingle() { }

            public static readonly DSingle Default = new DSingle();

            public override float ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadSingle();

            public override void WriteData( BinarySerializer w, float o ) => w.Write( o );

            public override void Export( float o, int num, ObjectExporter exporter ) => exporter.Target.EmitSingle( o );
        }

        public sealed class DDecimal : UnifiedTypeDriverBase<Decimal>
        {
            public DDecimal() { }

            public static readonly DDecimal Default = new DDecimal();

            public override decimal ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadDecimal();

            public override void WriteData( BinarySerializer w, decimal o ) => w.Write( o );

            public override void Export( decimal o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public sealed class DSByte : UnifiedTypeDriverBase<SByte>
        {
            public DSByte() { }

            public static readonly DSByte Default = new DSByte();

            public override sbyte ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadSByte();
            
            public override void WriteData( BinarySerializer w, sbyte o ) => w.Write( o );
            
            public override void Export( sbyte o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public sealed class DByte : UnifiedTypeDriverBase<Byte>
        {
            public DByte() { }

            public static readonly DByte Default = new DByte();

            public override byte ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadByte();

            public override void WriteData( BinarySerializer w, byte o ) => w.Write( o );
            
            public override void Export( byte o, int num, ObjectExporter exporter ) => exporter.Target.EmitByte( o );
        }

        public sealed class DInt16 : UnifiedTypeDriverBase<Int16>
        {
            public DInt16() { }

            public static readonly DInt16 Default = new DInt16();

            public override short ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadInt16();
            
            public override void WriteData( BinarySerializer w, short o ) => w.Write( o );

            public override void Export( short o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt16( o );
        }

        public sealed class DUInt16 : UnifiedTypeDriverBase<UInt16>
        {
            public DUInt16() { }

            public static readonly DUInt16 Default = new DUInt16();

            public override ushort ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadUInt16();

            public override void WriteData( BinarySerializer w, ushort o ) => w.Write( o );
            
            public override void Export( ushort o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt16( o );
        }

        public sealed class DInt32 : UnifiedTypeDriverBase<Int32>
        {
            public DInt32() { }

            public static readonly DInt32 Default = new DInt32();

            public override int ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadInt32();

            public override void WriteData( BinarySerializer w, int o ) =>  w.Write( o );

            public override void Export( int o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt32( o );
        }

        public sealed class DUInt32 : UnifiedTypeDriverBase<UInt32>
        {
            public DUInt32() { }

            public static readonly DUInt32 Default = new DUInt32();

            public override uint ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadUInt32();

            public override void WriteData( BinarySerializer w, uint o ) => w.Write( o );

            public override void Export( uint o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt32( o );
        }

        public sealed class DInt64 : UnifiedTypeDriverBase<Int64>
        {
            public DInt64() { }

            public static readonly DInt64 Default = new DInt64();

            public override long ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadInt64();

            public override void WriteData( BinarySerializer w, long o ) => w.Write( o );

            public override void Export( long o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt64( o );
        }

        public sealed class DUInt64 : UnifiedTypeDriverBase<UInt64>
        {
            public DUInt64() { }

            public static readonly DUInt64 Default = new DUInt64();

            public override ulong ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadUInt64();

            public override void WriteData( BinarySerializer w, ulong o ) => w.Write( o );

            public override void Export( ulong o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt64( o );
        }

        public sealed class DString : UnifiedTypeDriverBase<String>
        {
            public DString() { }

            public static readonly DString Default = new DString();

            public override string ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadNullableString();

            public override void WriteData( BinarySerializer w, string o ) => w.WriteNullableString( o );

            public override void Export( string o, int num, ObjectExporter exporter ) => exporter.Target.EmitString( o );
        }

        public sealed class DGuid : UnifiedTypeDriverBase<Guid>
        {
            public DGuid() { }

            public static readonly DGuid Default = new DGuid();

            public override Guid ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadGuid();

            public override void WriteData( BinarySerializer w, Guid o ) => w.Write( o );

            public override void Export( Guid o, int num, ObjectExporter exporter ) => exporter.Target.EmitGuid( o );
        }

        public sealed class DDateTime : UnifiedTypeDriverBase<DateTime>
        {
            public DDateTime() { }

            public static readonly DDateTime Default = new DDateTime();

            public override DateTime ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadDateTime();

            public override void WriteData( BinarySerializer w, DateTime o ) => w.Write( o );

            public override void Export( DateTime o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTime( o );
        }

        public sealed class DTimeSpan : UnifiedTypeDriverBase<TimeSpan>
        {
            public DTimeSpan() { }

            public static readonly DTimeSpan Default = new DTimeSpan();

            public override TimeSpan ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadTimeSpan();

            public override void WriteData( BinarySerializer w, TimeSpan o ) => w.Write( o );

            public override void Export( TimeSpan o, int num, ObjectExporter exporter ) => exporter.Target.EmitTimeSpan( o );
        }

        public sealed class DDateTimeOffset : UnifiedTypeDriverBase<DateTimeOffset>
        {
            public DDateTimeOffset() { }

            public static readonly DDateTimeOffset Default = new DDateTimeOffset();

            public override DateTimeOffset ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadDateTimeOffset();

            public override void WriteData( BinarySerializer w, DateTimeOffset o ) => w.Write( o );

            public override void Export( DateTimeOffset o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTimeOffset( o );
        }

    }
}
