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

        public static object SaveAndLoadObject( this IBasicTestHelper @this, object o, IServiceProvider serviceProvider = null, ISerializerResolver serializers = null, IDeserializerResolver deserializers = null )
        {
            return SaveAndLoadObject( @this, o, (x,w) => w.WriteObject( x ), r => r.ReadObject(), serializers, deserializers );
        }

        public static T SaveAndLoadObject<T>( this IBasicTestHelper @this, T o, Action<T,BinarySerializer> w, Func<BinaryDeserializer,T> r, ISerializerResolver serializers = null, IDeserializerResolver deserializers = null )
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

        public class DomainTestHandler : IDisposable
        {
            public DomainTestHandler( IActivityMonitor m, string domainName, IServiceProvider serviceProvider, bool startTimer )
            {
                ServiceProvider = serviceProvider;
                Domain = new ObservableDomain( m, domainName, startTimer, serviceProvider );
            }

            public IServiceProvider ServiceProvider { get; set; }

            public ObservableDomain Domain { get; private set; }

            public void Reload( IActivityMonitor m, bool idempotenceCheck = false, int pauseReloadMilliseconds = 0 )
            {
                if( idempotenceCheck ) ObservableDomain.IdempotenceSerializationCheck( m, Domain );
                Domain = MonitorTestHelper.TestHelper.SaveAndLoad( Domain, serviceProvider: ServiceProvider, debugMode: true, pauseMilliseconds: pauseReloadMilliseconds );
            }

            public void Dispose()
            {
                Domain.Dispose();
            }
        }

        public static DomainTestHandler CreateDomainHandler( this IMonitorTestHelper @this, string domainName, IServiceProvider? serviceProvider, bool startTimer )
        {
            return new DomainTestHandler( @this.Monitor, domainName, serviceProvider, startTimer );
        }

        static ObservableDomain SaveAndLoad( IActivityMonitor m,
                                             ObservableDomain domain,
                                             string? renamed,
                                             IServiceProvider? serviceProvider,
                                             bool debugMode,
                                             bool? startTimer,
                                             int pauseMilliseconds,
                                             bool skipDomainDispose )
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( m, s, leaveOpen: true, debugMode: debugMode );
                if( !skipDomainDispose ) domain.Dispose();
                System.Threading.Thread.Sleep( pauseMilliseconds );
                var d = new ObservableDomain( m, renamed ?? domain.DomainName, false, serviceProvider );
                s.Position = 0;
                d.Load( m, s, domain.DomainName, startTimer: startTimer );
                return d;
            }
        }

        public static ObservableDomain SaveAndLoad( this IMonitorTestHelper @this,
                                                    ObservableDomain domain,
                                                    string? renamed = null,
                                                    IServiceProvider? serviceProvider = null,
                                                    bool debugMode = true,
                                                    bool? startTimer = null,
                                                    int pauseMilliseconds = 0,
                                                    bool skipDomainDispose = false )
        {
            return SaveAndLoad( @this.Monitor, domain, renamed, serviceProvider, debugMode, startTimer, pauseMilliseconds, skipDomainDispose );
        }


    }
}
