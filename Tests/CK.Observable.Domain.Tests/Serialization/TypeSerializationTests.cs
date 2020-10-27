using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using CK.Core;
using static CK.Testing.MonitorTestHelper;
using CK.Observable;

namespace CK.Observable.Domain.Tests.Serialization
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
            Type backRW = TestHelper.SaveAndLoadObject( t, ( type, w ) => w.Write( type ), r => r.ReadType() );
            backRW.Should().BeSameAs( t );

            Type backO = (Type)TestHelper.SaveAndLoadObject( t );
            backO.Should().BeSameAs( t );

            Type backD = TestHelper.SaveAndLoadObject( t, ( type, w ) => BasicTypeDrivers.DType.Default.WriteData( w, type ), r => BasicTypeDrivers.DType.Default.ReadInstance( r, null ) );
            backD.Should().BeSameAs( t );
        }
    }
}
