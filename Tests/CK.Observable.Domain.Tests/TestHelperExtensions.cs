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

        /// <summary>
        /// Gets or sets whether SaveAndLoad methods check the reference management
        /// by writing a first (empty) object before and after the actual write, and yet another
        /// (empty) instance after twice (to use the reference tracking). 
        /// When reading, we first read this empty instance before the actual read, read it again
        /// and reads twice the second instance.
        /// <para>
        /// Defaults to true.
        /// </para>
        /// </summary>
        /// <param name="this">This tester.</param>
        /// <param name="check">Check the references.</param>
        public static void SetCheckObjectReferences( this IBasicTestHelper @this, bool check )
        {
            CheckObjectReferences = check;
        }


        [return: NotNullIfNotNull("o")]
        public static T SaveAndLoadObject<T>( this IBasicTestHelper @this, T o,
                                                                            BinarySerializerContext? serializerContext = null,
                                                                            BinaryDeserializerContext? deserializerContext = null ) where T : class
        {
            return SaveAndLoad( @this, o, ( x, w ) => w.WriteObject( x ), r => r.ReadObject<T>(), serializerContext, deserializerContext );
        }

        [return: NotNullIfNotNull("o")]
        public static object? SaveAndLoadAny( this IBasicTestHelper @this, object? o,
                                                                           BinarySerializerContext? serializerContext = null,
                                                                           BinaryDeserializerContext? deserializerContext = null )
        {
            return SaveAndLoad( @this, o, ( x, w ) => w.WriteAnyNullable( x ), r => r.ReadAnyNullable(), serializerContext, deserializerContext );
        }

        public static T SaveAndLoadValue<T>( this IBasicTestHelper @this, in T v,
                                                                          BinarySerializerContext? serializerContext = null,
                                                                          BinaryDeserializerContext? deserializerContext = null ) where T : struct
        {
            return SaveAndLoad<T>( @this, v, ( x, w ) => w.WriteValue( x ), r => r.ReadValue<T>(), serializerContext, deserializerContext );
        }

        public static T? SaveAndLoadNullableValue<T>( this IBasicTestHelper @this, in T? v,
                                                                                   BinarySerializerContext? serializerContext = null,
                                                                                   BinaryDeserializerContext? deserializerContext = null ) where T : struct
        {
            return SaveAndLoad<T?>( @this, v, ( x, w ) => w.WriteNullableValue( x ), r => r.ReadNullableValue<T>(), serializerContext, deserializerContext );
        }

        public static T SaveAndLoad<T>( this IBasicTestHelper @this, in T o,
                                                                     Action<T, BinarySerialization.IBinarySerializer> w,
                                                                     Func<BinarySerialization.IBinaryDeserializer, T> r,
                                                                     BinarySerializerContext? serializerContext = null,
                                                                     BinaryDeserializerContext? deserializerContext = null )
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerialization.BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
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
                return BinarySerialization.BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(), d =>
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

        public static void SaveAndLoad( this IBasicTestHelper @this, Action<BinarySerialization.IBinarySerializer> w,
                                                                     Action<BinarySerialization.IBinaryDeserializer> r,
                                                                     BinarySerializerContext? serializerContext = null,
                                                                     BinaryDeserializerContext? deserializerContext = null )
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerialization.BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
            {
                writer.DebugWriteSentinel();
                w( writer );
                writer.DebugWriteSentinel();
                s.Position = 0;
                BinarySerialization.BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(), d =>
                {
                    d.DebugCheckSentinel();
                    r( d );
                    d.DebugCheckSentinel();

                } ).ThrowOnInvalidResult();
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
                domain.Save( m, s, debugMode: debugMode );
                if( !skipDomainDispose ) domain.Dispose();
                System.Threading.Thread.Sleep( pauseMilliseconds );
                var d = new ObservableDomain( m, renamed ?? domain.DomainName, false, serviceProvider );
                s.Position = 0;
                d.Load( m, RewindableStream.FromStream( s ), domain.DomainName, startTimer: startTimer );
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
