using FluentAssertions;
using NUnit.Framework;
using System;

namespace CK.Observable.Domain.Tests;

[TestFixture]
public class ObservableObjectIdTests
{
    [Test]
    public void OId_is_double_compatible_so_that_an_ECMAScript_number_can_handle_it()
    {
        var id1 = new ObservableObjectId( id: 3712L, throwOnInvalid: true );
        Check( id1 );

        var id2 = new ObservableObjectId( idx: 3712, uniquifier: 42 );
        Check( id2 );

        var id3 = new ObservableObjectId( idx: ObservableObjectId.MaxIndexValue, uniquifier: ObservableObjectId.MaxUniquifierValue );
        Check( id3 );

        static void Check( ObservableObjectId id )
        {
            // To Javascript.
            // We COULD have done this but this NOT required: regular converting
            // the long ObservableObjectId.UniqueId from/to double is guaranteed to work.
            double numId = BitConverter.Int64BitsToDouble( id.UniqueId );

            // No Convert.ToDouble is required here:
            double numIdGentle = id.UniqueId;
            numId.Should().NotBe( numIdGentle );

            // From Javascript.
            var idBack = new ObservableObjectId( BitConverter.DoubleToInt64Bits( numId ), true );

            // Here, Convert.ToInt64 is required!
            var idBackGentle = new ObservableObjectId( Convert.ToInt64( numIdGentle ), true );

            idBack.UniqueId.Should().Be( id.UniqueId );
            idBack.Uniquifier.Should().Be( id.Uniquifier );

            idBackGentle.UniqueId.Should().Be( id.UniqueId );
            idBackGentle.Uniquifier.Should().Be( id.Uniquifier );

        }
    }
}
