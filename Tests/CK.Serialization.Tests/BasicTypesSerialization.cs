using CK.Observable;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using static CK.Testing.MonitorTestHelper;

namespace CK.Serialization.Tests
{
    [TestFixture]
    public class BasicTypesSerialization
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
        public static readonly DateTime DefDateTime = new DateTime( 2018, 9, 5, 16, 6, 47, DateTimeKind.Local );
        public static readonly TimeSpan DefTimeSpan = new TimeSpan( 3, 2, 1, 59, 995 );
        public static readonly DateTimeOffset DefDateTimeOffset = new DateTimeOffset( DefDateTime, DateTimeOffset.Now.Offset );
        public static readonly double DefDouble = 35.9783e-78;
        public static readonly float DefSingle = (float)0.38974e-4;
        public static readonly char DefChar = 'c';
        public static readonly bool DefBoolean = true;

        [Test]
        public void serialization_and_deserialization_of_basic_types()
        {
            using( var m = new MemoryStream() )
            using( BinarySerializer s = new BinarySerializer( m, leaveOpen: true ) )
            {
                int len = 0;
                s.Write( DefString ); len += 1 + DefString.Length; m.Position.Should().Be( len );
                s.Write( DefGuid ); len += 16; m.Position.Should().Be( len );
                s.Write( DefInt32 ); len += 4; m.Position.Should().Be( len );
                s.Write( DefUInt32 ); len += 4; m.Position.Should().Be( len );
                s.Write( DefInt64 ); len += 8; m.Position.Should().Be( len );
                s.Write( DefUInt64 ); len += 8; m.Position.Should().Be( len );
                s.Write( DefInt16 ); len += 2; m.Position.Should().Be( len );
                s.Write( DefUInt16 ); len += 2; m.Position.Should().Be( len );
                s.Write( DefByte ); len += 1; m.Position.Should().Be( len );
                s.Write( DefSByte ); len += 1; m.Position.Should().Be( len );
                s.Write( DefDateTime ); len += 8; m.Position.Should().Be( len );
                s.Write( DefTimeSpan ); len += 8; m.Position.Should().Be( len );
                s.Write( DefDateTimeOffset ); len += 8 + 2; m.Position.Should().Be( len );
                s.Write( DefDouble ); len += 8; m.Position.Should().Be( len );
                s.Write( DefSingle ); len += 4; m.Position.Should().Be( len );
                s.Write( DefChar ); len += 1; m.Position.Should().Be( len );
                s.Write( DefBoolean ); len += 1; m.Position.Should().Be( len );
            }
        }
    }
}
