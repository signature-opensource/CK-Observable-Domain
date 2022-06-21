using CK.BinarySerialization;
using CK.Observable;
using CK.Testing;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace CK.Core
{
    static class TestHelperExtensions
    {
        public static bool CheckObjectReferences = true;

        public static T SaveAndLoad<T>( this IBasicTestHelper @this, in T o,
                                                                     Action<T, IBinarySerializer> w,
                                                                     Func<IBinaryDeserializer, T> r,
                                                                     BinarySerializerContext? serializerContext = null,
                                                                     BinaryDeserializerContext? deserializerContext = null )
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
            {
                writer.DebugWriteMode( true );

                var o1 = new object();
                if( CheckObjectReferences )
                {
                    writer.WriteAny( o1 );
                }
                
                writer.DebugWriteSentinel();
                w( o, writer );
                writer.DebugWriteSentinel();

                if( CheckObjectReferences )
                {
                    writer.WriteAny( o1 );
                    var o2 = new object();
                    writer.WriteAny( o2 );
                    writer.WriteAny( o2 );
                }
                s.Position = 0;
                return BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(), d =>
                {
                    d.DebugReadMode();

                    object? r1 = null;
                    if( CheckObjectReferences )
                    {
                        r1 = d.ReadAny();
                    }

                    d.DebugCheckSentinel();
                    T result = r( d );
                    d.DebugCheckSentinel();

                    if( CheckObjectReferences )
                    {
                        d.ReadAny().Should().BeSameAs( r1 );
                        var r2 = d.ReadAny();
                        r2.Should().BeOfType<object>();
                        d.ReadAny().Should().BeSameAs( r2 );
                    }
                    return result;
                } ).GetResult();
            }
        }

        public static void SaveAndLoad( this IBasicTestHelper @this, Action<IBinarySerializer> w,
                                                                     Action<IBinaryDeserializer> r,
                                                                     BinarySerializerContext? serializerContext = null,
                                                                     BinaryDeserializerContext? deserializerContext = null )
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
            {
                writer.DebugWriteSentinel();
                w( writer );
                writer.DebugWriteSentinel();
                s.Position = 0;
                BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(), d =>
                {
                    d.DebugCheckSentinel();
                    r( d );
                    d.DebugCheckSentinel();

                } ).ThrowOnInvalidResult();
            }
        }

        public class DomainTestHandler : IDisposable
        {
            public DomainTestHandler( IActivityMonitor m, string domainName, IServiceProvider? serviceProvider, bool startTimer )
            {
                ServiceProvider = serviceProvider;
                Domain = new ObservableDomain( m, domainName, startTimer, serviceProvider );
            }

            public IServiceProvider? ServiceProvider { get; set; }

            public ObservableDomain Domain { get; private set; }

            /// <summary>
            /// Saves this <see cref="Domain"/>, disposes it and return a new domain from the saved bits.
            /// </summary>
            /// <param name="m">The monitor.</param>
            /// <param name="idempotenceCheck">True to call <see cref="ObservableDomain.IdempotenceSerializationCheck"/> on this domain first.</param>
            /// <param name="pauseReloadMilliseconds">Optional pause between reloading a new domain.</param>
            public void ReloadNewDomain( IActivityMonitor m, bool idempotenceCheck = false, int pauseReloadMilliseconds = 0 )
            {
                if( idempotenceCheck ) ObservableDomain.IdempotenceSerializationCheck( m, Domain );
                Domain = MonitorTestHelper.TestHelper.CloneDomain( Domain, serviceProvider: ServiceProvider, debugMode: true, pauseMilliseconds: pauseReloadMilliseconds );
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

        public static ObservableDomain CloneDomain( this IMonitorTestHelper @this,
                                                    ObservableDomain initial,
                                                    IServiceProvider? serviceProvider = null,
                                                    IObservableDomainClient? client = null,
                                                    bool? startTimer = null,
                                                    string? newName = null,
                                                    bool debugMode = true,
                                                    int pauseMilliseconds = 0,
                                                    bool initialDomainDispose = true )
        {
            return DoCloneDomain( @this,
                                  initial,
                                  ( monitor, newName, startTimer, c, s ) => new ObservableDomain( monitor, newName, startTimer, client, serviceProvider ),
                                  serviceProvider,
                                  client,
                                  startTimer,
                                  newName,
                                  debugMode,
                                  pauseMilliseconds,
                                  initialDomainDispose );
        }

        public static ObservableDomain<T> CloneDomain<T>( this IMonitorTestHelper @this,
                                                          ObservableDomain<T> initial,
                                                          IServiceProvider? serviceProvider = null,
                                                          IObservableDomainClient? client = null,
                                                          bool? startTimer = null,
                                                          string? newName = null,
                                                          bool debugMode = true,
                                                          int pauseMilliseconds = 0,
                                                          bool initialDomainDispose = true )
            where T : ObservableRootObject
        {
            return (ObservableDomain<T>)DoCloneDomain( @this,
                                                       initial,
                                                       ( monitor, newName, startTimer, c, s ) => new ObservableDomain<T>( monitor, newName, startTimer, c, s ),
                                                       serviceProvider,
                                                       client,
                                                       startTimer,
                                                       newName,
                                                       debugMode,
                                                       pauseMilliseconds,
                                                       initialDomainDispose );
        }

        public static ObservableDomain<T1,T2> CloneDomain<T1,T2>( this IMonitorTestHelper @this,
                                                              ObservableDomain<T1,T2> initial,
                                                              IServiceProvider? serviceProvider = null,
                                                              IObservableDomainClient? client = null,
                                                              bool? startTimer = null,
                                                              string? newName = null,
                                                              bool debugMode = true,
                                                              int pauseMilliseconds = 0,
                                                              bool initialDomainDispose = true )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
        {
            return (ObservableDomain<T1,T2>)DoCloneDomain( @this,
                                                           initial,
                                                           (monitor, newName, startTimer, c, s ) => new ObservableDomain<T1,T2>( monitor, newName, startTimer, c, s ),
                                                           serviceProvider,
                                                           client,
                                                           startTimer,
                                                           newName,
                                                           debugMode,
                                                           pauseMilliseconds,
                                                           initialDomainDispose );
        }

        static ObservableDomain DoCloneDomain( this IMonitorTestHelper @this,
                                               ObservableDomain initial,
                                               Func<IActivityMonitor, string, bool, IObservableDomainClient?, IServiceProvider?, ObservableDomain> factory,
                                               IServiceProvider? serviceProvider = null,
                                               IObservableDomainClient? client = null,
                                               bool? startTimer = null,
                                               string? newName = null,
                                               bool debugMode = true,
                                               int pauseMilliseconds = 0,
                                               bool initialDomainDispose = true )
        {
            using( var s = new MemoryStream() )
            {
                initial.Save( @this.Monitor, s, debugMode: debugMode );
                if( initialDomainDispose ) initial.Dispose();
                System.Threading.Thread.Sleep( pauseMilliseconds );
                var d = factory( @this.Monitor, newName ?? initial.DomainName, false, client, serviceProvider );
                s.Position = 0;
                using( var r = RewindableStream.FromStream( s ) )
                {
                    d.Load( @this.Monitor, r, initial.DomainName, startTimer: startTimer );
                }
                return d;
            }
        }

    }
}
