using CK.Observable;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;

namespace CK.Serialization.Tests
{
    [TestFixture]
    public class ArraySerializationTests
    {
        [Test]
        public void struct_types_array_serialization()
        {
            var positions = new Observable.Domain.Tests.Sample.Position[] { new Observable.Domain.Tests.Sample.Position( 12.2, 78.8 ), new Observable.Domain.Tests.Sample.Position( 44.7, 1214.777 ) };

            object back = SaveAndLoad( positions );
            back.Should().BeAssignableTo<Observable.Domain.Tests.Sample.Position[]>();
            var b = (Observable.Domain.Tests.Sample.Position[])back;
            b.Should().BeEquivalentTo( positions, options => options.WithStrictOrdering() );
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
