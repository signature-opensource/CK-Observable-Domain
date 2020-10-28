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
            public DomainTestHandler( IActivityMonitor m, string domainName, IServiceProvider serviceProvider )
            {
                ServiceProvider = serviceProvider;
                Domain = new ObservableDomain( m, domainName, serviceProvider );
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

        public static DomainTestHandler CreateDomainHandler( this IMonitorTestHelper @this, string domainName, IServiceProvider? serviceProvider )
        {
            return new DomainTestHandler( @this.Monitor, domainName, serviceProvider );
        }

        static ObservableDomain SaveAndLoad( IActivityMonitor m,
                                             ObservableDomain domain,
                                             string? renamed,
                                             IServiceProvider? serviceProvider,
                                             bool debugMode,
                                             Func<ObservableDomain, bool>? loadHook,
                                             int pauseMilliseconds,
                                             bool skipDomainDispose,
                                             SaveDisposedObjectBehavior saveDisposed )
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( m, s, leaveOpen: true, debugMode: debugMode, saveDisposed: saveDisposed );
                if( !skipDomainDispose ) domain.Dispose();
                System.Threading.Thread.Sleep( pauseMilliseconds );
                var d = new ObservableDomain( m, renamed ?? domain.DomainName, serviceProvider );
                s.Position = 0;
                d.Load( m, s, domain.DomainName, loadHook: loadHook );
                return d;
            }
        }

        public static ObservableDomain SaveAndLoad( this IMonitorTestHelper @this,
                                                    ObservableDomain domain,
                                                    string? renamed = null,
                                                    IServiceProvider? serviceProvider = null,
                                                    bool debugMode = true,
                                                    Func<ObservableDomain, bool>? loadHook = null,
                                                    int pauseMilliseconds = 0,
                                                    bool skipDomainDispose = false,
                                                    SaveDisposedObjectBehavior saveDisposed = SaveDisposedObjectBehavior.None )
        {
            return SaveAndLoad( @this.Monitor, domain, renamed, serviceProvider, debugMode, loadHook, pauseMilliseconds, skipDomainDispose, saveDisposed );
        }


    }
}
