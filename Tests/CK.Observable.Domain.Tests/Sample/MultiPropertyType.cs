using CK.Core;
using System;
using System.Diagnostics;

namespace CK.Observable.Domain.Tests.Sample
{
    [BinarySerialization.SerializationVersion(0)]
    public sealed class MultiPropertyType : ObservableObject, IEquatable<MultiPropertyType>
    {
        public static readonly string DefString = "MultiPropertyType";
        public static readonly Guid DefGuid = new Guid( "4F5E996D-51E9-4B04-B572-5126B14A5ECA" );
        public static readonly int DefInt32 = -42;
        public static readonly uint DefUInt32 = 42;
        public static readonly long DefInt64 = -42 << 48;
        public static readonly ulong DefUInt64 = 42 << 48;
        public static readonly short DefInt16 = -3712;
        public static readonly ushort DefUInt16 = 3712;
        public static readonly byte DefByte = 255;
        public static readonly sbyte DefSByte = -128;
        public static readonly DateTime DefDateTime = new DateTime( 2018, 9, 5, 16, 6, 47 );
        public static readonly TimeSpan DefTimeSpan = new TimeSpan( 3, 2, 1, 59, 995 );
        public static readonly DateTimeOffset DefDateTimeOffset = new DateTimeOffset( DefDateTime, DateTimeOffset.Now.Offset );
        public static readonly double DefDouble = 35.9783e-78;
        public static readonly float DefSingle = (float)0.38974e-4;
        public static readonly char DefChar = 'c';
        public static readonly bool DefBoolean = true;
        public static readonly Position DefPosition = new Position( 11.11, 22.22 );

        public MultiPropertyType()
        {
            SetDefaults();
        }


        public void SetDefaults()
        {
            String = DefString;
            Int32 = DefInt32;
            UInt32 = DefUInt32;
            Int64 = DefInt64;
            UInt64 = DefUInt64;
            Int16 = DefInt16;
            UInt16 = DefUInt16;
            Byte = DefByte;
            SByte = DefSByte;
            DateTime = DefDateTime;
            TimeSpan = DefTimeSpan;
            DateTimeOffset = DefDateTimeOffset;
            Guid = DefGuid;
            Double = DefDouble;
            Single = DefSingle;
            Char = DefChar;
            Boolean = DefBoolean;
            Position = DefPosition;
        }

        public void ChangeAll( string change, int delta, Guid g )
        {
            String = change;
            Int32 += delta;
            UInt32 = (uint)((int)UInt32 + delta);
            Int64 += delta;
            UInt64 = (ulong)((long)UInt64 + delta);
            Int16 = (short)((short)Int16 + delta);
            UInt16 = (ushort)((short)UInt16 + delta);
            Byte = (byte)(Byte + delta);
            SByte += (sbyte)(SByte + delta);
            DateTime = DateTime.AddDays( delta );
            TimeSpan = TimeSpan.Add( TimeSpan.FromHours( delta ) );
            DateTimeOffset = DateTimeOffset.Add( TimeSpan.FromMinutes( delta ) );
            Guid = g;
            Double += delta;
            Single += delta;
            Char = (char)(Char + delta);
            Boolean = (delta&1) == 0;
            Enum = (MechanicLevel)(((int)Enum + delta) % (int)(MechanicLevel.Master + 1));
            Position = new Position( Position.Latitude + delta, Position.Longitude + delta );
        }

        MultiPropertyType( BinarySerialization.IBinaryDeserializer s, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            var r = s.Reader;
            String = r.ReadNullableString();
            Int32 = r.ReadInt32();
            UInt32 = r.ReadUInt32();
            Int64 = r.ReadInt64();
            UInt64 = r.ReadUInt64();
            Int16 = r.ReadInt16();
            UInt16 = r.ReadUInt16();
            Byte = r.ReadByte();
            SByte = r.ReadSByte();
            DateTime = r.ReadDateTime();
            TimeSpan = r.ReadTimeSpan();
            DateTimeOffset = r.ReadDateTimeOffset();
            Guid = r.ReadGuid();
            Double = r.ReadDouble();
            Single = r.ReadSingle();
            Char = r.ReadChar();
            Boolean = r.ReadBoolean();

            // Enum can be serialized through their drivers:
            Enum = s.ReadValue<MechanicLevel>();
            // Or directly (simpler, more efficient but the underlying type cannot be changed directly):
            var e = (MechanicLevel)r.ReadInt32();
            Debug.Assert( e == Enum );

            Position = s.ReadValue<Position>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in MultiPropertyType o )
        {
            var w = s.Writer;
            w.WriteNullableString( o.String );
            w.Write( o.Int32 );
            w.Write( o.UInt32 );
            w.Write( o.Int64 );
            w.Write( o.UInt64 );
            w.Write( o.Int16 );
            w.Write( o.UInt16 );
            w.Write( o.Byte );
            w.Write( o.SByte );
            w.Write( o.DateTime );
            w.Write( o.TimeSpan );
            w.Write( o.DateTimeOffset );
            w.Write( o.Guid );
            w.Write( o.Double );
            w.Write( o.Single );
            w.Write( o.Char );
            w.Write( o.Boolean );

            // See the deserialization part.
            s.WriteValue( o.Enum );
            w.Write( (int)o.Enum );

            s.WriteValue( o.Position );
        }

        public override bool Equals( object obj ) => obj is MultiPropertyType m && Equals( m );

        public override int GetHashCode()
        {
            return HashCode.Combine(
                    String,
                    Int32,
                    UInt32,
                    Int64,
                    UInt64,
                    HashCode.Combine( SByte,
                        Int16,
                        UInt16,
                        Byte,
                        DateTime,
                        TimeSpan,
                        DateTimeOffset,
                        Guid ),
                    HashCode.Combine(
                        Double,
                        Single,
                        Char,
                        Boolean,
                        Position.Latitude,
                        Position.Longitude,
                        Enum ) );
        }

        public bool Equals( MultiPropertyType other )
        {
            return String == other.String &&
                    Int32 == other.Int32 &&
                    UInt32 == other.UInt32 &&
                    Int64 == other.Int64 &&
                    UInt64 == other.UInt64 &&
                    Int16 == other.Int16 &&
                    UInt16 == other.UInt16 &&
                    Byte == other.Byte &&
                    SByte == other.SByte &&
                    DateTime == other.DateTime &&
                    TimeSpan == other.TimeSpan &&
                    DateTimeOffset == other.DateTimeOffset &&
                    Guid == other.Guid &&
                    Double == other.Double &&
                    Single == other.Single &&
                    Char == other.Char &&
                    Boolean == other.Boolean &&
                    Position.Latitude == other.Position.Latitude &&
                    Position.Longitude == other.Position.Longitude &&
                    Enum == other.Enum;
        }

        public string String { get; set; }

        public int Int32 { get; set; }

        public uint UInt32 { get; set; }

        public long Int64 { get; set; }

        public ulong UInt64 { get; set; }

        public short Int16 { get; set; }

        public ushort UInt16 { get; set; }

        public byte Byte { get; set; }

        public sbyte SByte { get; set; }

        public DateTime DateTime { get; set; }

        public TimeSpan TimeSpan { get; set; }

        public DateTimeOffset DateTimeOffset { get; set; }

        public Guid Guid { get; set; }

        public double Double { get; set; }

        public float Single { get; set; }

        public char Char { get; set; }

        public bool Boolean { get; set; }

        public Position Position { get; set; }

        public MechanicLevel Enum { get; set; }
    }
}
