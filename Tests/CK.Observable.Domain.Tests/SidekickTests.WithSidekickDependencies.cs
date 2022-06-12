using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    public partial class SidekickTests
    {
        public class ExternalService
        {
            public string Format( string msg ) => $"Formatted by ExternalService: '{msg}'.";
        }

        public class SKWithDependencies : ObservableDomainSidekick
        {
            readonly ExternalService _s;

            public SKWithDependencies( IActivityMonitor ctorMonitor, IObservableDomainSidekickManager manager, ExternalService s )
                : base( manager )
            {
                _s = s;
                ctorMonitor.Info( "Initializing SKWithDependencies." );
            }

            protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
            {
                o.GetType().Name.Should().Be( "ObjWithSKWithDependenciesViaInterface" );
                monitor.Info( "ISidekickClientObject<T> registers the objects that implements it." );
            }

            protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
            {
                if( command.Command is CmdSimple c )
                {
                    monitor.Info( $"SKWithDependencies[{_s.Format( c.Text ?? "no text" )}]" );
                    return true;
                }
                return false;
            }

            protected override void OnUnload( IActivityMonitor monitor )
            {
            }

        }

        [UseSidekick( typeof( SKWithDependencies ) )]
        [SerializationVersion( 0 )]
        public sealed class ObjWithSKWithDependencies : ObjWithSKBase
        {
            public ObjWithSKWithDependencies()
            {
            }

            ObjWithSKWithDependencies( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKWithDependencies o )
            {
            }
        }

        [SerializationVersion( 0 )]
        public sealed class ObjWithSKWithDependenciesViaInterface : ObjWithSKBase, ISidekickClientObject<SKWithDependencies>
        {
            public ObjWithSKWithDependenciesViaInterface()
            {
            }

            ObjWithSKWithDependenciesViaInterface( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKWithDependenciesViaInterface o )
            {
            }
        }


        [TestCase( "UseSidekickAttribute" )]
        [TestCase( "ISidekickClientObject<>" )]
        public void sidekick_with_ExternalService( string mode )
        {
            var services = new ServiceCollection()
                                .AddSingleton<ExternalService>()
                                .BuildServiceProvider();

            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null!;

            using var obs = new ObservableDomain( TestHelper.Monitor, nameof( sidekick_with_ExternalService ) + '_' + mode, startTimer: true, serviceProvider: services );

            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    ObjWithSKBase o = mode == "UseSidekickAttribute"
                                            ? (ObjWithSKBase)new ObjWithSKWithDependencies()
                                            : new ObjWithSKWithDependenciesViaInterface();
                    o.CommandMessage = "Hello!";
                } );
                t.Success.Should().BeTrue();
            }
            logs.Select( l => l.Text ).Should().Contain( "Initializing SKWithDependencies." );
            logs.Select( l => l.Text ).Should().Contain( "SKWithDependencies[Formatted by ExternalService: 'Hello!'.]" );
            if( mode == "ISidekickClientObject<>" )
            {
                logs.Select( l => l.Text ).Should().Contain( "ISidekickClientObject<T> registers the objects that implements it." );
            }
        }

    }

}
