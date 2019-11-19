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
                writer.DebugWriteSentinel();
                w( o, writer );
                writer.DebugWriteSentinel();
                s.Position = 0;
                using( var reader = new BinaryDeserializer( s, null, deserializers ) )
                {
                    reader.DebugCheckSentinel();
                    T result = r( reader );
                    reader.DebugCheckSentinel();
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

        public class DomainHandler : IDisposable
        {
            public DomainHandler( IActivityMonitor m, string domainName )
            {
                Domain = new ObservableDomain( m, domainName );
            }

            public ObservableDomain Domain { get; private set; }

            public void Reload( IActivityMonitor m )
            {
                var d = SaveAndLoad( m, Domain, Domain.DomainName, debugMode: true );
                Domain.Dispose();
                Domain = d;
            }

            public void Dispose()
            {
                Domain.Dispose();
            }
        }

        public static DomainHandler CreateDomainHandler( this IMonitorTestHelper @this, string domainName )
        {
            return new DomainHandler( @this.Monitor, domainName );
        }

        static ObservableDomain SaveAndLoad( IActivityMonitor m, ObservableDomain domain, string renamed, bool debugMode )
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( m, s, leaveOpen: true, debugMode );
                var d = new ObservableDomain( m, renamed ?? domain.DomainName );
                s.Position = 0;
                d.Load( m, s, domain.DomainName );
                return d;
            }
        }

        public static ObservableDomain SaveAndLoad( this IMonitorTestHelper @this, ObservableDomain domain, string renamed = null, bool debugMode = true )
        {
            return SaveAndLoad( @this.Monitor, domain, renamed, debugMode );
        }


    }
}