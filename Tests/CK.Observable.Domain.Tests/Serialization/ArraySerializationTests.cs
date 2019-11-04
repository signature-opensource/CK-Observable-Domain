using CK.Observable;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using CK.Core;
using static CK.Testing.MonitorTestHelper;

namespace CK.Serialization.Tests
{
    [TestFixture]
    public class ArraySerializationTests
    {
        [Test]
        public void struct_types_array_serialization()
        {
            var positions = new Observable.Domain.Tests.Sample.Position[] { new Observable.Domain.Tests.Sample.Position( 12.2, 78.8 ), new Observable.Domain.Tests.Sample.Position( 44.7, 1214.777 ) };

            object back = TestHelper.SaveAndLoad( positions );
            back.Should().BeAssignableTo<Observable.Domain.Tests.Sample.Position[]>();
            var b = (Observable.Domain.Tests.Sample.Position[])back;
            b.Should().BeEquivalentTo( positions, options => options.WithStrictOrdering() );
        }

        [Test]
        public void object_array_serialization()
        {
            object[] objects = new object[] { 12, "Pouf", 10.90, new DateTime( 2018, 9, 17 ) };

            object back = TestHelper.SaveAndLoad( objects );
            back.Should().BeAssignableTo<object[]>();
            object[] b = (object[])back;
            b.Should().BeEquivalentTo( objects, options => options.WithStrictOrdering() );
        }

        [Test]
        public void type_array_serialization()
        {
            Type[] types = new Type[] { GetType(), typeof(int), typeof(string), typeof(IActivityLogGroup) };

            {
                Type[] back = (Type[])TestHelper.SaveAndLoad( types );
                back.Should().BeEquivalentTo( types, options => options.WithStrictOrdering() );
            }
            {
                Type[] back = TestHelper.SaveAndLoad( types,
                                                      (t,w) => ArraySerializer<Type>.WriteObjects( w, types.Length, types, BasicTypeDrivers.DType.Default ),
                                                      r => ArrayDeserializer<Type>.ReadArray( r, BasicTypeDrivers.DType.Default ) );
                back.Should().BeEquivalentTo( types, options => options.WithStrictOrdering() );
            }

        }

    }
}
