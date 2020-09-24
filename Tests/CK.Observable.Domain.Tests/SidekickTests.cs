using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class SidekickTests
    {
        public class CmdSimple { public string Text { get; set; } }

        public class SKSimple : ObservableDomainSidekick
        {
            public SKSimple( ObservableDomain d )
                : base( d )
            {
            }

            protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
            {
                if( command.Command is CmdSimple c )
                {
                    monitor.Info( $"Handling [{c.Text}]" );
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

        [UseSidekick( typeof( SKSimple ) )]
        [SerializationVersion( 0 )]
        public class ObjWithSKSimple : ObservableObject
        {
            public ObjWithSKSimple()
            {
            }

            protected ObjWithSKSimple( IBinaryDeserializerContext c )
                : base( c )
            {
                var r = c.StartReading();
            }

            void Write( BinarySerializer s )
            {
            }

            public string Message { get; set; }


            void OnMessageChanged( object before, object after )
            {
                if( Domain.IsDeserializing ) return;
                Domain.SendCommand( new CmdSimple() { Text = Message } );
            }

        }

        [Test]

        public void sidekick_simple_instantiation_and_serialization()
        {
            var obs = new ObservableDomain( TestHelper.Monitor, nameof( sidekick_simple_instantiation_and_serialization ) );

            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null;
            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObjWithSKSimple();
                    obs.AllObjects.Should().HaveCount( 1 );
                    o.Message = "Hello!";
                } );
                t.Success.Should().BeTrue();
            }
            logs.SingleOrDefault( l => l.Text == "Handling [Hello!]" ).Should().NotBeNull();
            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    var another = new ObjWithSKSimple();
                    obs.AllObjects.Should().HaveCount( 2 );
                    var o = (ObjWithSKSimple)obs.AllObjects.First();
                    o.Message = "FromO";
                    another.Message = "FromA";
                } );
                t.Success.Should().BeTrue();
            }
            logs.SingleOrDefault( l => l.Text == "Handling [FromO]" ).Should().NotBeNull();
            logs.SingleOrDefault( l => l.Text == "Handling [FromA]" ).Should().NotBeNull();

            var obs2 = TestHelper.SaveAndLoad( obs );
            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs2.Modify( TestHelper.Monitor, () =>
                {
                    var o = (ObjWithSKSimple)obs2.AllObjects.First();
                    var a = (ObjWithSKSimple)obs2.AllObjects.Skip(1).First();
                    o.Message = "O!";
                    a.Message = "A!";
                } );
                t.Success.Should().BeTrue();
            }
            logs.SingleOrDefault( l => l.Text == "Handling [O!]" ).Should().NotBeNull();
            logs.SingleOrDefault( l => l.Text == "Handling [A!]" ).Should().NotBeNull();
        }

        public class ExternalService
        {
            public string Format( string msg ) => $"Formatted by ExternalService: '{msg}'.";
        }

        public class SKWithDependencies : ObservableDomainSidekick
        {
            readonly ExternalService _s;

            public SKWithDependencies( IActivityMonitor ctorMonitor, ObservableDomain d, ExternalService s )
                : base( d )
            {
                _s = s;
                ctorMonitor.Info( "Initializing SKWithDependencies." );
            }
            protected override void RegisterClientObject( IActivityMonitor monitor, IDisposableObject o )
            {
            }

            protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
            {
                if( command.Command is CmdSimple c )
                {
                    monitor.Info( $"SKWithDependencies[{_s.Format( c.Text )}]" );
                    return true;
                }
                return false;
            }

            protected override void Dispose( IActivityMonitor monitor )
            {
            }

        }


        [UseSidekick( typeof( SKWithDependencies ) )]
        [SerializationVersion( 0 )]
        public class ObjWithSKWithDependencies : ObservableObject
        {
            public ObjWithSKWithDependencies()
            {
            }

            protected ObjWithSKWithDependencies( IBinaryDeserializerContext c )
                : base( c )
            {
                var r = c.StartReading();
            }

            void Write( BinarySerializer s )
            {
            }

            public string Message { get; set; }


            void OnMessageChanged( object before, object after )
            {
                if( Domain.IsDeserializing ) return;
                Domain.SendCommand( new CmdSimple() { Text = Message } );
            }

        }

        [Test]
        public void sidekick_with_ExternalService()
        {
            var services = new ServiceCollection()
                                .AddSingleton<ExternalService>()
                                .BuildServiceProvider();

            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null;

            var obs = new ObservableDomain( TestHelper.Monitor, nameof( sidekick_with_ExternalService ), services );
            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObjWithSKWithDependencies();
                    o.Message = "Hello!";
                } );
                t.Success.Should().BeTrue();
            }
            logs.Select( l => l.Text ).Should().Contain( "Initializing SKWithDependencies." );
            logs.Select( l => l.Text ).Should().Contain( "SKWithDependencies[Formatted by ExternalService: 'Hello!'.]" );
        }
    }

}
