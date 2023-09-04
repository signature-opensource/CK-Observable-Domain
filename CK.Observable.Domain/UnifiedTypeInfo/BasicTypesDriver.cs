using System;
using CK.Core;

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

            public override void Export( object o, int num, ObjectExporter exporter ) => exporter.ExportObject( o );

        }

        public sealed class DType : UnifiedTypeDriverBase<Type>
        {
            public DType() { }

            public static readonly DType Default = new DType();

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

            public override void Export( bool o, int num, ObjectExporter exporter ) => exporter.Target.EmitBool( o );
        }

        public sealed class DChar : UnifiedTypeDriverBase<Char>
        {
            public DChar() { }

            public static readonly DChar Default = new DChar();
            
            public override void Export( char o, int num, ObjectExporter exporter ) => exporter.Target.EmitChar( o );
        }

        public sealed class DDouble : UnifiedTypeDriverBase<Double>
        {
            public DDouble() { }

            public static readonly DDouble Default = new DDouble();

            public override void Export( double o, int num, ObjectExporter exporter ) => exporter.Target.EmitDouble( o );
        }

        public sealed class DSingle : UnifiedTypeDriverBase<Single>
        {
            public DSingle() { }

            public static readonly DSingle Default = new DSingle();

            public override void Export( float o, int num, ObjectExporter exporter ) => exporter.Target.EmitSingle( o );
        }

        public sealed class DDecimal : UnifiedTypeDriverBase<Decimal>
        {
            public DDecimal() { }

            public static readonly DDecimal Default = new DDecimal();

            public override void Export( decimal o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public sealed class DSByte : UnifiedTypeDriverBase<SByte>
        {
            public DSByte() { }

            public static readonly DSByte Default = new DSByte();
                        
            public override void Export( sbyte o, int num, ObjectExporter exporter ) => exporter.Target.EmitSByte( o );
        }

        public sealed class DByte : UnifiedTypeDriverBase<Byte>
        {
            public DByte() { }

            public static readonly DByte Default = new DByte();
            
            public override void Export( byte o, int num, ObjectExporter exporter ) => exporter.Target.EmitByte( o );
        }

        public sealed class DInt16 : UnifiedTypeDriverBase<Int16>
        {
            public DInt16() { }

            public static readonly DInt16 Default = new DInt16();
            
            public override void Export( short o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt16( o );
        }

        public sealed class DUInt16 : UnifiedTypeDriverBase<UInt16>
        {
            public DUInt16() { }

            public static readonly DUInt16 Default = new DUInt16();
            
            public override void Export( ushort o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt16( o );
        }

        public sealed class DInt32 : UnifiedTypeDriverBase<Int32>
        {
            public DInt32() { }

            public static readonly DInt32 Default = new DInt32();

            public override void Export( int o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt32( o );
        }

        public sealed class DUInt32 : UnifiedTypeDriverBase<UInt32>
        {
            public DUInt32() { }

            public static readonly DUInt32 Default = new DUInt32();

            public override void Export( uint o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt32( o );
        }

        public sealed class DInt64 : UnifiedTypeDriverBase<Int64>
        {
            public DInt64() { }

            public static readonly DInt64 Default = new DInt64();

            public override void Export( long o, int num, ObjectExporter exporter ) => exporter.Target.EmitInt64( o );
        }

        public sealed class DUInt64 : UnifiedTypeDriverBase<UInt64>
        {
            public DUInt64() { }

            public static readonly DUInt64 Default = new DUInt64();

            public override void Export( ulong o, int num, ObjectExporter exporter ) => exporter.Target.EmitUInt64( o );
        }

        public sealed class DString : UnifiedTypeDriverBase<String>
        {
            public DString() { }

            public static readonly DString Default = new DString();

            public override void Export( string o, int num, ObjectExporter exporter ) => exporter.Target.EmitString( o );
        }

        public sealed class DGuid : UnifiedTypeDriverBase<Guid>
        {
            public DGuid() { }

            public static readonly DGuid Default = new DGuid();

            public override void Export( Guid o, int num, ObjectExporter exporter ) => exporter.Target.EmitGuid( o );
        }

        public sealed class DDateTime : UnifiedTypeDriverBase<DateTime>
        {
            public DDateTime() { }

            public static readonly DDateTime Default = new DDateTime();

            public override void Export( DateTime o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTime( o );
        }

        public sealed class DTimeSpan : UnifiedTypeDriverBase<TimeSpan>
        {
            public DTimeSpan() { }

            public static readonly DTimeSpan Default = new DTimeSpan();

            public override void Export( TimeSpan o, int num, ObjectExporter exporter ) => exporter.Target.EmitTimeSpan( o );
        }

        public sealed class DDateTimeOffset : UnifiedTypeDriverBase<DateTimeOffset>
        {
            public DDateTimeOffset() { }

            public static readonly DDateTimeOffset Default = new DDateTimeOffset();

            public override void Export( DateTimeOffset o, int num, ObjectExporter exporter ) => exporter.Target.EmitDateTimeOffset( o );
        }

        public sealed class DNormalizedPath : UnifiedTypeDriverBase<NormalizedPath>
        {
            public DNormalizedPath() { }

            public static readonly DNormalizedPath Default = new DNormalizedPath();

            public override void Export( NormalizedPath o, int num, ObjectExporter exporter ) => exporter.Target.EmitString( o );
        }
    }
}
