using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{

    [TestFixture]
    public class PostActionsTests
    {
        public enum HandlerTarget { Local, Domain }

        public class SimpleCommand
        {
            public HandlerTarget Target { get; set; }

            public int Number { get; set; }

            public bool UseAsync { get; set; }

            public int WaitTime { get; set; }

            public override string ToString() => $"{Target}: {Number}";
        }

        static int Sequence = -1;
        static int NextSequence() => Interlocked.Increment( ref Sequence );
        static readonly ConcurrentQueue<(int Sequence, int Number)> DomainNumbers = new ConcurrentQueue<(int Sequence, int Number)>();
        static readonly ConcurrentQueue<(int Sequence, int Number)> LocalNumbers = new ConcurrentQueue<(int Sequence, int Number)>();
        static void ResetContext()
        {
            Sequence = -1;
            DomainNumbers.Clear();
            LocalNumbers.Clear();
        }

        public class SimpleSidekick : ObservableDomainSidekick
        {
            public SimpleSidekick( ObservableDomain d )
                : base( d )
            {
            }

            protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
            {
                if( command.Command is SimpleCommand c )
                {
                    var q = c.Target == HandlerTarget.Local ? LocalNumbers : DomainNumbers;
                    var sender = c.Target == HandlerTarget.Local ? command.LocalPostActions : command.DomainPostActions;
                    if( c.UseAsync )
                    {
                        sender.Add( (Func<PostActionContext, Task>)(async ctx =>
                        {
                            q.Enqueue( (NextSequence(), c.Number) );
                            if( c.WaitTime > 0 ) await Task.Delay( c.WaitTime );
                        }) );
                    }
                    else
                    {
                        sender.Add( ctx =>
                        {
                            q.Enqueue( (NextSequence(), c.Number) );
                            if( c.WaitTime > 0 ) Thread.Sleep( c.WaitTime );
                        } );
                    }
                    return true;
                }
                return false;
            }

            protected override void RegisterClientObject( IActivityMonitor monitor, IDisposableObject o )
            {
            }

            protected override void Dispose( IActivityMonitor monitor )
            {
            }


        }

        [UseSidekick(typeof(SimpleSidekick))]
        public class SimpleRoot : ObservableRootObject
        {
            int _domainNumber;
            int _localNumber;

            public void SendNumber( HandlerTarget t, bool useAsync = false ) => Domain.SendCommand( CreateCommand( t, useAsync ) );

            public void CreateNewTimers( int nbTimers, HandlerTarget target, int ms, int maxCount )
            {
                var now = DateTime.UtcNow;
                for( int i = 0; i < nbTimers; ++i )
                {
                    var t = new ObservableTimer( now, ms ) { Tag = ( target, maxCount ) };
                    t.Elapsed += OnElapsed;
                }
            }

            SimpleCommand CreateCommand( HandlerTarget t, bool useAsync )
            {
                return new SimpleCommand() { Target = t, UseAsync = useAsync, Number = t == HandlerTarget.Local ? _localNumber-- : _domainNumber++ };
            }

            void OnElapsed( object sender, ObservableTimerEventArgs e )
            {
                var tag = (ValueTuple<HandlerTarget,int>)e.Timer.Tag;
                if( tag.Item2-- == 0 )
                {
                    e.Timer.Dispose();
                }
                else
                {
                    SendNumber( tag.Item1 );
                }
                e.Timer.Tag = tag;
            }
        }

        [Test]
        public async Task local_are_executed_before_domain_ones()
        {
            ResetContext();

            using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor, "local_are_executed_before_domain_ones", startTimer: true );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.SendNumber( HandlerTarget.Local );
                d.Root.SendNumber( HandlerTarget.Domain );
                d.Root.SendNumber( HandlerTarget.Local );
                d.Root.SendNumber( HandlerTarget.Domain );
                d.Root.SendNumber( HandlerTarget.Local );
                d.Root.SendNumber( HandlerTarget.Domain );
            } );
            LocalNumbers.Should().BeEquivalentTo( (0, 0), (1, -1), (2, -2) );
            DomainNumbers.Should().BeEquivalentTo( (3, 0), (4, 1), (5, 2) );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.SendNumber( HandlerTarget.Local );
                d.Root.SendNumber( HandlerTarget.Domain );
                d.Root.SendNumber( HandlerTarget.Local );
                d.Root.SendNumber( HandlerTarget.Domain );
                d.Root.SendNumber( HandlerTarget.Local );
                d.Root.SendNumber( HandlerTarget.Domain );
            } );
            LocalNumbers.Should().BeEquivalentTo( (0, 0), (1, -1), (2, -2), (6, -3), (7, -4), (8, -5) );
            DomainNumbers.Should().BeEquivalentTo( (3, 0), (4, 1), (5, 2), (9, 3), (10, 4), (11, 5) );
        }

        [TestCase( 2, false )]
        [TestCase( 20, false )]
        [TestCase( 2, true )]
        [TestCase( 20, true )]
        public async Task parrallel_operations_respect_the_Domain_PostActions_oredering_guaranty( int nb, bool useAsync )
        {
            ResetContext();

            using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor, "parrallel_operations_respect_the_Domain_PostActions_oredering_guaranty", startTimer: true );

            Barrier b = new Barrier( nb );
            var tasks = Enumerable.Range( 0, nb ).Select( i => Task.Run( () => Run( i == 0 ? TestHelper.Monitor : new ActivityMonitor(), d, b ) ) ).ToArray();
            await Task.WhenAll( tasks );

            LocalNumbers.Select( x => x.Number ).Should().NotBeInAscendingOrder();
            DomainNumbers.Select( x => x.Number ).Should().BeInAscendingOrder();

            Task Run( IActivityMonitor monitor, ObservableDomain<SimpleRoot> d, Barrier b )
            {
                b.SignalAndWait();
                return d.ModifyThrowAsync( monitor, () =>
                {
                    d.Root.SendNumber( HandlerTarget.Local, useAsync );
                    d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                    d.Root.SendNumber( HandlerTarget.Local, useAsync );
                    d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                    d.Root.SendNumber( HandlerTarget.Local, useAsync );
                    d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                } );
            }
        }

        [TestCase( 2, 2, false )]
        [TestCase( 4, 4, true )]
        public async Task multiple_Timers_respect_the_Domain_PostActions_oredering_guaranty( int nb, int nbTimers, bool useAsync )
        {
            ResetContext();

            using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor, "multiple_Timers_respect_the_Domain_PostActions_oredering_guaranty", startTimer: true );

            Barrier b = new Barrier( nb );
            var tasks = Enumerable.Range( 0, nb ).Select( i => Task.Run( () => Run( i == 0 ? TestHelper.Monitor : new ActivityMonitor(), d, b ) ) ).ToArray();
            await Task.WhenAll( tasks );

            await Task.Delay( nb * 2 * nbTimers * 20 * 20 );

            // Security: allow to wait twice the time...
            if( LocalNumbers.Count != nb * (1 + nbTimers * 20)
                || DomainNumbers.Count != nb * (1 + nbTimers * 20) )
            {
                await Task.Delay( nb * 2 * nbTimers * 20 * 20 );
            }


            LocalNumbers.Count.Should().Be( nb * ( 1 + nbTimers * 20) );
            DomainNumbers.Count.Should().Be( nb * ( 1 + nbTimers * 20) );

            LocalNumbers.Select( x => x.Number ).Should().NotBeInAscendingOrder();
            DomainNumbers.Select( x => x.Number ).Should().BeInAscendingOrder();

            Task Run( IActivityMonitor monitor, ObservableDomain<SimpleRoot> d, Barrier b )
            {
                b.SignalAndWait();
                return d.ModifyThrowAsync( monitor, () =>
                {
                    d.Root.SendNumber( HandlerTarget.Local, useAsync );
                    d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                    d.Root.CreateNewTimers( nbTimers, HandlerTarget.Local, 20, 20 );
                    d.Root.CreateNewTimers( nbTimers, HandlerTarget.Domain, 20, 20 );
                } );
            }
        }

    }
}
