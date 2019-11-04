using CK.Observable;
using CK.Testing;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace CK.Core
{
    static class TestHelperExtensions
    {

        public static object SaveAndLoad( this IBasicTestHelper @this, object o, ISerializerResolver serializers = null, IDeserializerResolver deserializers = null )
        {
            return SaveAndLoad( @this, o, (x,w) => w.WriteObject( x ), r => r.ReadObject(), serializers, deserializers );
        }

        public static T SaveAndLoad<T>( this IBasicTestHelper @this, T o, Action<T,BinarySerializer> w, Func<BinaryDeserializer,T> r, ISerializerResolver serializers = null, IDeserializerResolver deserializers = null )
        {
            using( var s = new MemoryStream() )
            using( var writer = new BinarySerializer( s, serializers, true ) )
            {
                w( o, writer );
                writer.Write( "sentinel" );
                s.Position = 0;
                using( var reader = new BinaryDeserializer( s, null, deserializers ) )
                {
                    T result = r( reader );
                    reader.ReadString().Should().Be( "sentinel" );
                    return result;
                }
            }
        }

        public static object SaveAndLoadViaStandardSerialization( this IBasicTestHelper @this, object o )
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
