using CK.Observable;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Serialization.Tests
{
    [TestFixture]
    public class ArraySerializationTests
    {
        [Test]
        public void basic_types_array_serialization()
        {
            int[] integers = new int[] { 12, 11, 10, 9, 8 };

            object back = SaveAndLoad( integers );
            back.Should().BeAssignableTo<int[]>();
            int[] b = (int[])back;
            b.Should().BeEquivalentTo( integers, options => options.WithStrictOrdering() );
        }

        [Test]
        public void object_array_serialization()
        {
            object[] objects = new object[] { 12, "Pouf", 10.90, new DateTime( 2018, 9, 17 ) };

            object back = SaveAndLoad( objects );
            back.Should().BeAssignableTo<object[]>();
            object[] b = (object[])back;
            b.Should().BeEquivalentTo( objects, options => options.WithStrictOrdering() );
        }

        internal static object SaveAndLoad( object o, ISerializerResolver serializers = null, IDeserializerResolver deserializers = null )
        {
            using( var s = new MemoryStream() )
            using( var w = new BinarySerializer( s, serializers, true ) )
            {
                w.WriteObject( o );
                s.Position = 0;
                using( var r = new BinaryDeserializer( s, null, deserializers ) )
                {
                    return r.ReadObject();
                }
            }
        }
    }
}
