using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests
{
     [TestFixture]
    public class DictionarySerializationTests
    {
        [Test]
        public void standard_serialization()
        {
            var d = new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase );
            d.Add( "A", 1 );
            d["a"].Should().Be( 1 );

            var d2 = (Dictionary<string, int>)SaveAndLoadStandard( d );
            d2["a"].Should().Be( 1 );
            d2.Add( "B", 2 );
            d2["b"].Should().Be( 2 );

            d2.Comparer.Should().NotBeSameAs( StringComparer.InvariantCultureIgnoreCase );
        }

        internal static object SaveAndLoadStandard( object o )
        {
            using( var s = new MemoryStream() )
            {
                new BinaryFormatter().Serialize( s, o );
                s.Position = 0;
                return new BinaryFormatter().Deserialize( s );
            }
        }

    }
}
