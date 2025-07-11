using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using Shouldly;
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


namespace CK.Observable.Domain.Tests;


[TestFixture]
public class PostActionsTests
{
    [SetUp]
    public void BeforeEach()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public enum HandlerTarget { Local, Domain }

    public class SimpleCommand
    {
        public HandlerTarget Target { get; set; }

        public int Number { get; set; }

        public bool UseAsync { get; set; }

        public int ExecutionWaitTime { get; set; }

        public override string ToString() => $"{Target}: {Number}";
    }

    public enum WaitTarget
    {
        CommandHandler,
        PostActions,
        DomainPostActions
    }

    public class WaitCommand
    {
        public int WaitTime { get; set; }

        public WaitTarget Target { get; set; }
    }

    static int Sequence = -1;
    static int NextSequence() => Interlocked.Increment( ref Sequence );
    static readonly ConcurrentQueue<(int Sequence, int Number)> DomainNumbers = new ConcurrentQueue<(int Sequence, int Number)>();
    static readonly ConcurrentQueue<(int Sequence, int Number)> LocalNumbers = new ConcurrentQueue<(int Sequence, int Number)>();
    static int ErrorNumber = 0;
    static int NextErrorNumber() => Interlocked.Increment( ref ErrorNumber );
    static void ResetContext()
    {
        Sequence = -1;
        ErrorNumber = 0;
        DomainNumbers.Clear();
        LocalNumbers.Clear();
    }

    public class SimpleSidekick : ObservableDomainSidekick
    {
        public SimpleSidekick( IObservableDomainSidekickManager manager )
            : base( manager )
        {
        }

        protected override void OnTransactionResult( TransactionDoneEventArgs result )
        {
            if( result.Events.Count > 0 && result.Events.Any( e => e.EventType == ObservableEventType.ListInsert ) )
            {
                throw new CKException( $"Error in OnSuccessfulTransaction event ({NextErrorNumber()})." );
            }
        }

        protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
        {
            if( command.Command is WaitCommand w )
            {
                if( w.Target == WaitTarget.CommandHandler )
                {
                    monitor.Info( $"Waiting for {w.WaitTime} ms in CommandHandler." );
                    Thread.Sleep( w.WaitTime );
                }
                else
                {
                    var sender = w.Target == WaitTarget.PostActions ? command.PostActions : command.DomainPostActions;
                    sender.Add( ctx =>
                    {
                        ctx.Monitor.Info( $"Waiting for {w.WaitTime} ms in {w.Target}." );
                        Thread.Sleep( w.WaitTime );
                    } );
                }
                return true;
            }
            if( command.Command is SimpleCommand c )
            {
                var q = c.Target == HandlerTarget.Local ? LocalNumbers : DomainNumbers;
                var sender = c.Target == HandlerTarget.Local ? command.PostActions : command.DomainPostActions;
                if( c.Number == -2 )
                {
                    throw new CKException( $"Error in ExecuteCommand ({NextErrorNumber()})." );
                }
                if( c.UseAsync )
                {
                    sender.Add( (Func<PostActionContext, Task>)(async ctx =>
                    {
                        if( c.Number == -3 ) throw new CKException( $"Error in Command processing {c.Target} ({NextErrorNumber()})." );
                        int w = c.ExecutionWaitTime;
                        if( w > 0 ) await Task.Delay( w );
                        q.Enqueue( (NextSequence(), c.Number) );
                    }) );
                }
                else
                {
                    sender.Add( ctx =>
                    {
                        if( c.Number == -3 ) throw new CKException( $"Error in Command processing {c.Target} ({NextErrorNumber()})." );
                        int w = c.ExecutionWaitTime;
                        if( w > 0 ) Thread.Sleep( w );
                        q.Enqueue( (NextSequence(), c.Number) );
                    } );
                }
                return true;
            }
            return false;
        }

        protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
        {
        }

        protected override void OnUnload( IActivityMonitor monitor )
        {
        }


    }

    [UseSidekick(typeof(SimpleSidekick))]
    [SerializationVersion(0)]
    public class SimpleRoot : ObservableRootObject
    {
        int _domainNumber;
        int _localNumber;

        public SimpleRoot()
        {
        }

        SimpleRoot( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in SimpleRoot o )
        {
        }

        public ObservableChannel<string> ErrorOnSuccefulTransactionError { get; } = new ObservableChannel<string>();

        public void SendNumber( HandlerTarget t, bool useAsync = false ) => Domain.SendBroadcastCommand( CreateCommand( t, useAsync ) );

        public void RaiseError( int number )
        {
            if( number == -1 )
            {
                ErrorOnSuccefulTransactionError.Send( "Pouf!" );
            }
            else
            {
                // The target is for number = -3 (Local processor) and -4 (Domain processor).
                var t = HandlerTarget.Local;
                if( number == -4 )
                {
                    t = HandlerTarget.Domain;
                    number = -3;
                }
                Domain.SendBroadcastCommand( new SimpleCommand() { Target = t, Number = number } );
            }
        }

        public void SendWait( int time, WaitTarget target ) => Domain.SendBroadcastCommand( new WaitCommand { WaitTime = time, Target = target } );

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
            return new SimpleCommand() { Target = t, UseAsync = useAsync, Number = t == HandlerTarget.Local ? 1000 + _localNumber++ : _domainNumber++ };
        }

        void OnElapsed( object sender, ObservableTimerEventArgs e )
        {
            var tag = (ValueTuple<HandlerTarget,int>)e.Timer.Tag!;
            if( tag.Item2-- == 0 )
            {
                e.Timer.Destroy();
            }
            else
            {
                SendNumber( tag.Item1 );
            }
            e.Timer.Tag = tag;
        }
    }

    [Test]
    public async Task local_are_executed_before_domain_ones_when_parallelDomainPostActions_is_false_Async()
    {
        ResetContext();

        using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor,
                                                        "local_are_executed_before_domain_ones_when_parallelDomainPostActions_is_false",
                                                        startTimer: true );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.Root.SendNumber( HandlerTarget.Local );
            d.Root.SendNumber( HandlerTarget.Domain );
            d.Root.SendNumber( HandlerTarget.Local );
            d.Root.SendNumber( HandlerTarget.Domain );
            d.Root.SendNumber( HandlerTarget.Local );
            d.Root.SendNumber( HandlerTarget.Domain );
        }, parallelDomainPostActions: false, waitForDomainPostActionsCompletion: true );

        LocalNumbers.ShouldBe( new[] { (0, 1000), (1, 1001), (2, 1002) } );
        DomainNumbers.ShouldBe( new[] { (3, 0), (4, 1), (5, 2) } );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.Root.SendNumber( HandlerTarget.Local );
            d.Root.SendNumber( HandlerTarget.Domain );
            d.Root.SendNumber( HandlerTarget.Local );
            d.Root.SendNumber( HandlerTarget.Domain );
            d.Root.SendNumber( HandlerTarget.Local );
            d.Root.SendNumber( HandlerTarget.Domain );
        }, parallelDomainPostActions: false, waitForDomainPostActionsCompletion: true );

        LocalNumbers.ShouldBe( [(0, 1000), (1, 1001), (2, 1002), (6, 1003), (7, 1004), (8, 1005)] );
        DomainNumbers.ShouldBe( [(3, 0), (4, 1), (5, 2), (9, 3), (10, 4), (11, 5)] );
    }

    [TestCase( 20, false )]
    [TestCase( 20, true )]
    [Timeout(30*1000)]
    public async Task parrallel_operations_respect_the_Domain_PostActions_ordering_guaranty_Async( int nb, bool useAsync )
    {
        ResetContext();

        using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor, $"parrallel_operations_respect_the_Domain_PostActions_ordering_guaranty-{nb}-{useAsync}", startTimer: true );

        Barrier b = new Barrier( nb );
        var tasks = Enumerable.Range( 0, nb ).Select( i => Task.Run( () => Run( i, i == 0 ? TestHelper.Monitor : new ActivityMonitor(), d, b ) ) ).ToArray();
        await Task.WhenAll( tasks );
        TestHelper.Monitor.Info( $"{nb} tasks done! Disposing Domain." );
        d.Dispose( TestHelper.Monitor );


        LocalNumbers.Count.ShouldBe( 3 * nb );
        // No Shouldly ShouldNotBeInOrder.
        LocalNumbers.Select( x => x.Number ).IsSortedLarge().ShouldBeFalse();

        DomainNumbers.Count.ShouldBe( 3 * nb );
        DomainNumbers.Select(x => x.Number).ShouldBeInOrder();

        async Task Run( int num, IActivityMonitor monitor, ObservableDomain<SimpleRoot> d, Barrier b )
        {
            monitor.Info( $"Run {num}: Waiting for Barrier..." );
            b.SignalAndWait();
            monitor.Info( $"Running {num}!" );
            var tr = await d.ModifyThrowAsync( monitor, () =>
            {
                d.Root.SendWait( num, WaitTarget.PostActions );
                d.Root.SendWait( num, WaitTarget.DomainPostActions );
                d.Root.SendNumber( HandlerTarget.Local, useAsync );
                d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                d.Root.SendNumber( HandlerTarget.Local, useAsync );
                d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                d.Root.SendNumber( HandlerTarget.Local, useAsync );
                d.Root.SendNumber( HandlerTarget.Domain, useAsync );
            } );
            monitor.Info( $"Run {num}: Waiting for DomainPostActionsError..." );
            await tr.DomainPostActionsError;
            monitor.Info( $"Awaited DomainPostActionsError! (Run {num})" );
        }
    }

    [TestCase( 2, 2, false )]
    [TestCase( 4, 4, true )]
    public async Task multiple_Timers_respect_the_Domain_PostActions_ordering_guaranty_Async( int nb, int nbTimers, bool useAsync )
    {
        ResetContext();

        using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor, "multiple_Timers_respect_the_Domain_PostActions_ordering_guaranty", startTimer: true );

        Barrier b = new Barrier( nb );
        var tasks = Enumerable.Range( 0, nb ).Select( i => Task.Run( () => Run( i, i == 0 ? TestHelper.Monitor : new ActivityMonitor(), d, b ) ) ).ToArray();
        await Task.WhenAll( tasks );

        await Task.Delay( nb * 2 * nbTimers * 20 * 20 + 100/*Security*/ );

        // Security: allow to wait twice the time...
        if( LocalNumbers.Count != nb * (1 + nbTimers * 20)
            || DomainNumbers.Count != nb * (1 + nbTimers * 20) )
        {
            await Task.Delay( nb * 2 * nbTimers * 20 * 20 );
        }

        DomainNumbers.Count.ShouldBe( nb * ( 1 + nbTimers * 20) );
        LocalNumbers.Count.ShouldBe( nb * ( 1 + nbTimers * 20) );

        DomainNumbers.Select(x => x.Number).ShouldBeInOrder();
        // No Shouldly ShouldNotBeInOrder.
        LocalNumbers.Select(x => x.Number).IsSortedLarge().ShouldBeFalse();

        Task Run( int num, IActivityMonitor monitor, ObservableDomain<SimpleRoot> d, Barrier b )
        {
            b.SignalAndWait();
            return d.ModifyThrowAsync( monitor, () =>
            {
                if( (num % 2) == 0 ) d.Root.SendWait( 500, WaitTarget.PostActions );
                d.Root.SendNumber( HandlerTarget.Local, useAsync );
                d.Root.SendNumber( HandlerTarget.Domain, useAsync );
                d.Root.CreateNewTimers( nbTimers, HandlerTarget.Local, 20, 20 );
                d.Root.CreateNewTimers( nbTimers, HandlerTarget.Domain, 20, 20 );
            } );
        }
    }

    [TestCase( 50, true )]
    [TestCase( 49, false )]
    public async Task parrallel_operations_with_random_behavior_Async( int nb, bool tryModifyAsync )
    {
        ResetContext();

        using var d = new ObservableDomain<SimpleRoot>( TestHelper.Monitor, "parrallel_operations_with_random_behavior" + nb, startTimer: true );

        var tasks = Enumerable.Range( 0, nb ).Select( i => Task.Run( () =>
        {
            if( tryModifyAsync )
            {
                return d.TryModifyAsync( i == 0 ? TestHelper.Monitor : new ActivityMonitor(), () => RunLoop( d ) );
            }
            else
            {
                return SafeModifyAsync( i == 0 ? TestHelper.Monitor : new ActivityMonitor(), d );
            }
        }) ).ToArray();
        await Task.WhenAll( tasks );

        DomainNumbers.Select(x => x.Number).ShouldBeInOrder();
        // There's no ShouldNotBeInOrder.
        LocalNumbers.Select( x => x.Number ).IsSortedLarge().ShouldBeFalse();

        static async Task SafeModifyAsync( IActivityMonitor monitor, ObservableDomain<SimpleRoot> d )
        {
            try
            {
                await d.ModifyThrowAsync( monitor, () => RunLoop( d ) );
            }
            catch( Exception )
            {
            }
        }

        static void RunLoop( ObservableDomain<SimpleRoot> d )
        {
            var random = new Random();
            for( int i = 0; i < 10; ++i )
            {
                if( random.Next( 10 ) == 0 )
                {
                    d.Root.SendWait( random.Next( 100 ), WaitTarget.CommandHandler );
                }
                if( random.Next( 10 ) == 0 )
                {
                    d.Root.SendWait( random.Next( 100 ), WaitTarget.PostActions );
                }
                if( random.Next( 10 ) == 0 )
                {
                    d.Root.SendWait( random.Next( 100 ), WaitTarget.DomainPostActions );
                }

                if( random.Next( 20 ) == 0 )
                {
                    // Sometimes we raise an error at the different steps:
                    // 0 - In the modify.
                    // 1 - In the OnSuccessfulTransaction handler.
                    // 2 - In the command handling.
                    // 3 - In the Local processor.
                    // 4 - In the Domain processor.
                    var error = -random.Next( 5 );
                    if( error == 0 ) throw new CKException( $"An error in Modify ({NextErrorNumber()})." );
                    d.Root.RaiseError( error );
                }
                else
                {
                    int distrib = random.Next( 4 );
                    if( distrib == 0 )
                    {
                        // Sometimes we emit only locals.
                        d.Root.SendNumber( HandlerTarget.Local );
                        d.Root.SendNumber( HandlerTarget.Local );
                    }
                    else if( distrib == 1 )
                    {
                        // Sometimes we emit only domains.
                        d.Root.SendNumber( HandlerTarget.Domain );
                        d.Root.SendNumber( HandlerTarget.Domain );
                    }
                    else if( distrib == 2 )
                    {
                        // Sometimes we emit nothing or both.
                        if( random.Next( 2 ) == 0 )
                        {
                            d.Root.SendNumber( HandlerTarget.Local );
                            d.Root.SendNumber( HandlerTarget.Domain );
                        }
                    }
                    else
                    {
                        // and sometimes we emit Local xor Domain.
                        if( random.Next( 2 ) == 0 )
                        {
                            d.Root.SendNumber( HandlerTarget.Domain );
                        }
                        else
                        {
                            d.Root.SendNumber( HandlerTarget.Local );
                        }
                    }
                }
            }
        }
    }

}
