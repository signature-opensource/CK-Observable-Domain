using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ObservableEventHandlerTests
    {

        #region Structure of ObservableDelegate & ObservableEventHandler

        /// <summary>
        /// Mimics the internal ObservableDelegate.
        /// </summary>
        struct D
        {
            // This is the internal delegate.
            // This MUST NOT be readonly.
            public object Ref;

            public void Set( object r ) => Ref = r;
            public bool IsSet => Ref != null;
        }

        /// <summary>
        /// Mimics the ObservableEventHandler that adapts the
        /// ObservableDelegate (in terms of delegate type).
        /// </summary>
        struct OE
        {
            /// <summary>
            /// The wrapped ObservableDelegate MUST NOT be readonly either.
            /// </summary>
            public D Del;

            public void Set( object r ) => Del.Set( r );
            public bool IsSet => Del.IsSet;
        }

        [Test]
        public void ObservableDelegate_and_ObservableEventHandler_are_structs()
        {
            var e = new OE();
            e.IsSet.Should().BeFalse();
            e.Set( this );
            e.IsSet.Should().BeTrue();
        }

        #endregion


        static EventArgs EArgs = EventArgs.Empty;
        static EventMonitoredArgs MArgs = new EventMonitoredArgs( TestHelper.Monitor );

        class MoreSpecializedEventArgs : EventMonitoredArgs
        {
            public MoreSpecializedEventArgs( IActivityMonitor m ) : base( m ) {}
        }

        static MoreSpecializedEventArgs OArgs = new MoreSpecializedEventArgs( TestHelper.Monitor );

        [Test]
        public void ObservableEventHandler_variance_when_delegate_type_fits()
        {
            {
                var h = new ObservableEventHandler<EventArgs>();
                h.HasHandlers.Should().BeFalse();
                h.Add( StaticOnEvent, nameof( StaticOnEvent ) );
                h.HasHandlers.Should().BeTrue();
                h.Invoking( _ => _.Raise( this, EArgs ) )
                    .Should().Throw<Exception>().WithMessage( "Such an ugly test...(EventArgs)" );
            }
            {
                var h = new ObservableEventHandler<EventMonitoredArgs>();
                h.HasHandlers.Should().BeFalse();
                h.Add( StaticOnEvent, nameof( StaticOnEvent ) );
                h.HasHandlers.Should().BeTrue();
                h.Invoking( _ => _.Raise( this, MArgs ) )
                    .Should().Throw<Exception>().WithMessage( "Such an ugly test...(EventMonitoredArgs)" );
            }
            {
                var h = new ObservableEventHandler<MoreSpecializedEventArgs>();
                h.HasHandlers.Should().BeFalse();
                h.Add( StaticOnEvent, nameof( StaticOnEvent ) );
                h.HasHandlers.Should().BeTrue();
                h.Invoking( _ => _.Raise( this, OArgs ) )
                    .Should().Throw<Exception>().WithMessage( "Such an ugly test...(MoreSpecializedEventArgs)" );
            }
        }

        static void StaticOnEvent( object sender, EventArgs e )
        {
            throw new Exception( "Such an ugly test...(EventArgs)" );
        }
        static void StaticOnEvent( object sender, EventMonitoredArgs e )
        {
            throw new Exception( "Such an ugly test...(EventMonitoredArgs)" );
        }
        static void StaticOnEvent( object sender, MoreSpecializedEventArgs e )
        {
            throw new Exception( "Such an ugly test...(MoreSpecializedEventArgs)" );
        }

        [Test]
        public void ObservableEventHandler_serialization_when_delegate_type_fits()
        {
            {
                var h = new ObservableEventHandler<EventArgs>();
                h.Add( StaticOnEvent, nameof( StaticOnEvent ) );
                {
                    var h2 = TestHelper.SaveAndLoad( h, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<EventArgs>( r ) );
                    h2.HasHandlers.Should().BeTrue();
                    h2.Invoking( _ => _.Raise( this, EArgs ) )
                                            .Should().Throw<Exception>().WithMessage( "Such an ugly test...(EventArgs)" );
                }
                h.RemoveAll();
                {
                    var h2 = TestHelper.SaveAndLoad( h, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<EventArgs>( r ) );
                    h2.HasHandlers.Should().BeFalse();
                    h2.Invoking( _ => _.Raise( this, EArgs ) )
                                            .Should().NotThrow();
                }
            }
            {
                var h = new ObservableEventHandler<EventMonitoredArgs>();
                h.Add( StaticOnEvent, nameof( StaticOnEvent ) );
                {
                    var h2 = TestHelper.SaveAndLoad( h, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<EventMonitoredArgs>( r ) );
                    h2.HasHandlers.Should().BeTrue();
                    h2.Invoking( _ => _.Raise( this, MArgs ) )
                                            .Should().Throw<Exception>().WithMessage( "Such an ugly test...(EventMonitoredArgs)" );
                }
                h.RemoveAll();
                {
                    var h2 = TestHelper.SaveAndLoad( h, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<EventMonitoredArgs>( r ) );
                    h2.HasHandlers.Should().BeFalse();
                    h2.Invoking( _ => _.Raise( this, MArgs ) )
                                            .Should().NotThrow();
                }
            }
            {
                var h = new ObservableEventHandler<MoreSpecializedEventArgs>();
                h.Add( StaticOnEvent, nameof( StaticOnEvent ) );
                {
                    var h2 = TestHelper.SaveAndLoad( h, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<MoreSpecializedEventArgs>( r ) );
                    h2.HasHandlers.Should().BeTrue();
                    h2.Invoking( _ => _.Raise( this, OArgs ) )
                                            .Should().Throw<Exception>().WithMessage( "Such an ugly test...(MoreSpecializedEventArgs)" );
                }
                h.RemoveAll();
                {
                    var h2 = TestHelper.SaveAndLoad( h, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<MoreSpecializedEventArgs>( r ) );
                    h2.HasHandlers.Should().BeFalse();
                    h2.Invoking( _ => _.Raise( this, OArgs ) )
                                            .Should().NotThrow();
                }
            }
        }

        static int _typeCalled = 0;

        static void StaticOnEventEventArgs( object sender, EventArgs e )
        {
            _typeCalled++;
        }

        static void StaticOnEventEventMonitoredArgs( object sender, EventMonitoredArgs e )
        {
            _typeCalled++;
        }

        static void StaticOnEventObservableTimedEventArgs( object sender, MoreSpecializedEventArgs e )
        {
            _typeCalled++;
        }

        [Test]
        public void ObservableEventHandler_serialization_when_delegate_type_must_be_adapted()
        {
            // Base type: nothing specific here (serialization when type fits is already tested by other tests).
            var hE = new ObservableEventHandler<EventArgs>();
            hE.Add( StaticOnEventEventArgs, nameof( StaticOnEventEventArgs ) );
            // hE.Add( StaticOnEventEventMonitoredArgs, nameof( StaticOnEventEventMonitoredArgs ) );
            // hE.Add( StaticOnEventObservableTimedEventArgs, nameof( StaticOnEventObservableTimedEventArgs ) );

            // Specialized event type: can be bound to basic EventArgs method.
            var hM = new ObservableEventHandler<EventMonitoredArgs>();
            hM.Add( StaticOnEventEventArgs, nameof( StaticOnEventEventArgs ) );
            hM.Add( StaticOnEventEventMonitoredArgs, nameof( StaticOnEventEventMonitoredArgs ) );
            // hE.Add( StaticOnEventObservableTimedEventArgs, nameof( StaticOnEventObservableTimedEventArgs ) );

            // Even more specialized event type.
            var hO = new ObservableEventHandler<MoreSpecializedEventArgs>();
            hO.Add( StaticOnEventEventArgs, nameof( StaticOnEventEventArgs ) );
            hO.Add( StaticOnEventEventMonitoredArgs, nameof( StaticOnEventEventMonitoredArgs ) );
            hO.Add( StaticOnEventObservableTimedEventArgs, nameof( StaticOnEventObservableTimedEventArgs ) );

            {
                _typeCalled = 0;
                var hM2 = TestHelper.SaveAndLoad( hM, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<EventMonitoredArgs>( r ) );
                hM2.HasHandlers.Should().BeTrue();
                hM2.Raise( this, MArgs );
                _typeCalled.Should().Be( 2 );
            }
            {
                _typeCalled = 0;
                var hO2 = TestHelper.SaveAndLoad( hO, ( x, w ) => x.Write( w ), r => new ObservableEventHandler<MoreSpecializedEventArgs>( r ) );
                hO2.HasHandlers.Should().BeTrue();
                hO2.Raise( this, OArgs );
                _typeCalled.Should().Be( 3 );
            }
        }

        [Test]
        public void testing_auto_cleanup()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, nameof( testing_auto_cleanup ) ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    TestCounter counter = new TestCounter();
                    Sample.Car c = new Sample.Car( "First Car" );
                    c.TestSpeedChanged += counter.Increment;

                    counter.Count.Should().Be( 0 );
                    c.TestSpeed = 89;
                    counter.Count.Should().Be( 1 );
                    counter.Dispose();
                    c.TestSpeed = 1;
                    counter.Count.Should().Be( 1 );

                    counter = new TestCounter();
                    var counter2 = new TestCounter();
                    c.TestSpeedChanged += counter.Increment;
                    c.TestSpeedChanged += counter2.Increment;
                    counter.Count.Should().Be( 0 );
                    counter2.Count.Should().Be( 0 );

                    c.TestSpeed = 89;
                    counter.Count.Should().Be( 1 );
                    counter2.Count.Should().Be( 1 );
                    counter.Dispose();
                    c.TestSpeed = 1;
                    counter.Count.Should().Be( 1 );
                    counter2.Count.Should().Be( 2 );
                    counter2.Dispose();
                    c.TestSpeed = 2;
                    counter.Count.Should().Be( 1 );
                    counter2.Count.Should().Be( 2 );

                    counter = new TestCounter();
                    counter2 = new TestCounter();
                    var counter3 = new TestCounter();
                    c.TestSpeedChanged += counter.Increment;
                    c.TestSpeedChanged += counter2.Increment;
                    c.TestSpeedChanged += counter3.Increment;
                    counter.Count.Should().Be( 0 );
                    counter2.Count.Should().Be( 0 );
                    counter3.Count.Should().Be( 0 );

                    c.TestSpeed = 89;
                    counter.Count.Should().Be( 1 );
                    counter2.Count.Should().Be( 1 );
                    counter3.Count.Should().Be( 1 );

                    counter2.Dispose();
                    c.TestSpeed = 1;
                    counter.Count.Should().Be( 2 );
                    counter2.Count.Should().Be( 1 );
                    counter3.Count.Should().Be( 2 );

                    counter3.Dispose();
                    c.TestSpeed = 2;
                    counter.Count.Should().Be( 3 );
                    counter2.Count.Should().Be( 1 );
                    counter3.Count.Should().Be( 2 );

                    counter.Dispose();
                    c.TestSpeed = 3;
                    counter.Count.Should().Be( 3 );
                    counter2.Count.Should().Be( 1 );
                    counter3.Count.Should().Be( 2 );
                } ).Success
                    .Should().BeTrue();
            }

        }

    }
}
