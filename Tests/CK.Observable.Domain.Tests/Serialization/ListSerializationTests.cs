using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using CK.Core;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Serialization
{
    [TestFixture]
    public class ListSerializationTests
    {
        [Test]
        public void basic_types_list_serialization()
        {
            var integers = new List<int>{ 12, 11, 10, 9, 8 };

            object back = TestHelper.SaveAndLoadObject( integers );
            back.Should().BeAssignableTo<List<int>>();
            var b = (List<int>)back;
            b.Should().BeEquivalentTo( integers, options => options.WithStrictOrdering() );
        }

        [Test]
        public void object_list_serialization()
        {
            var objects = new List<object>{ 12, "Pouf", 10.90, new DateTime( 2018, 9, 17 ) };

            object back = TestHelper.SaveAndLoadObject( objects );
            back.Should().BeAssignableTo<List<object>>();
            var b = (List<object>)back;
            b.Should().BeEquivalentTo( objects, options => options.WithStrictOrdering() );
        }
    }
}
