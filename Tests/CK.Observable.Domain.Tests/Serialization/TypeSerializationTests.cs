using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using CK.Core;
using static CK.Testing.MonitorTestHelper;
using CK.Observable;

namespace CK.Serialization.Tests
{
    [TestFixture]
    public class TypeSerializationTests
    {
        class TypeHolder<T> { }

        [TestCase( null )]
        [TestCase( typeof( TypeSerializationTests ) )]
        [TestCase( typeof( TypeHolder<Func<List<int>,double,Action<object>>> ) )]
        public void Type_serialization( Type t )
        {
            Type backRW = TestHelper.SaveAndLoad( t, ( type, w ) => w.Write( type ), r => r.ReadType() );
            backRW.Should().BeSameAs( t );

            Type backO = (Type)TestHelper.SaveAndLoad( t );
            backO.Should().BeSameAs( t );

            Type backD = TestHelper.SaveAndLoad( t, ( type, w ) => w.Write( type, BasicTypeDrivers.DType.Default ), r => r.Read( BasicTypeDrivers.DType.Default ) );
            backD.Should().BeSameAs( t );
        }
    }
}