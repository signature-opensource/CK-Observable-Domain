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
        public class CmdSimple { public string? Text { get; set; } }

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

            protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
            {
                monitor.Info( $"Registered: {o.GetType().Name}." );
            }

            protected override void OnUnload( IActivityMonitor monitor )
            {
            }


        }

        [SerializationVersion( 0 )]
        public class ObjWithSKBase : ObservableObject
        {
            public ObjWithSKBase()
            {
                Message = "";
            }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            protected ObjWithSKBase( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618 

            ObjWithSKBase( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                Message = r.Reader.ReadString();
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKBase o )
            {
                s.Writer.Write( o.Message );
            }

            public string Message { get; set; }


            void OnMessageChanged( object before, object after )
            {
                if( Domain.IsDeserializing ) return;
                Domain.SendCommand( new CmdSimple() { Text = Message } );
            }

        }

        /// <summary>
        /// When using the attribute, the object is not registered onto the sidekick.
        /// If the sidekick needs to know some of the objects of the domain, it has to discover them
        /// (typically through the <see cref="ObservableDomain.AllObjects"/>).
        /// </summary>
        [UseSidekick( typeof( SKSimple ) )]
        [SerializationVersion( 0 )]
        public class ObjWithSKSimple : ObjWithSKBase
        {
            public ObjWithSKSimple()
            {
            }

            ObjWithSKSimple( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKSimple o )
            {
            }
        }

        /// <summary>
        /// When using the ISidekickClientObject<>, the object is automatically registered on the sidekick.
        /// </summary>
        [SerializationVersion( 0 )]
        public class ObjWithSKSimpleViaInterface : ObjWithSKBase, ISidekickClientObject<SKSimple>
        {
            public ObjWithSKSimpleViaInterface()
            {
            }

            ObjWithSKSimpleViaInterface( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKSimpleViaInterface o )
            {
            }
        }

        #region Same 2 objects as above but Internal rather than Observable.
        [SerializationVersion( 0 )]
        public class InternalObjWithSKBase : InternalObject
        {
            string _message;

            public InternalObjWithSKBase()
            {
                _message = "";
            }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            protected InternalObjWithSKBase( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618 

            InternalObjWithSKBase( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                _message = r.Reader.ReadString();
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in InternalObjWithSKBase o )
            {
                s.Writer.Write( o._message );
            }

            public string Message
            {
                get => _message;
                set
                {
                    if( _message != value )
                    {
                        _message = value;
                        Domain.SendCommand( new CmdSimple() { Text = _message } );
                    }

                }
            }

        }

        /// <summary>
        /// When using the attribute, the object is not registered onto the sidekick.
        /// If the sidekick needs to know some of the objects of the domain, it has to discover them
        /// (typically through the <see cref="ObservableDomain.AllObjects"/>).
        /// </summary>
        [UseSidekick( typeof( SKSimple ) )]
        [SerializationVersion( 0 )]
        public class InternalObjWithSKSimple : InternalObjWithSKBase
        {
            public InternalObjWithSKSimple()
            {
            }

            InternalObjWithSKSimple( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in InternalObjWithSKSimple o )
            {
            }
        }

        /// <summary>
        /// When using the ISidekickClientObject<>, the object is automatically registered on the sidekick.
        /// </summary>
        [SerializationVersion( 0 )]
        public class InternalObjWithSKSimpleViaInterface : InternalObjWithSKBase, ISidekickClientObject<SKSimple>
        {
            public InternalObjWithSKSimpleViaInterface()
            {
            }

            InternalObjWithSKSimpleViaInterface( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in InternalObjWithSKSimpleViaInterface o )
            {
            }
        }
        #endregion

        [TestCase( "UseSidekickAttribute", "ObservableObject" )]
        [TestCase( "ISidekickClientObject<>", "ObservableObject" )]
        [TestCase( "UseSidekickAttribute", "InternalObject" )]
        [TestCase( "ISidekickClientObject<>", "InternalObject" )]
        public void sidekick_simple_instantiation_and_serialization( string mode, string type )
        {
            // Will be disposed by TestHelper.SaveAndLoad at the end of this test.
            var obs = new ObservableDomain( TestHelper.Monitor, nameof( sidekick_simple_instantiation_and_serialization ) + '-' + mode, startTimer: true );

            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null!;
            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    if( type == "ObservableObject" )
                    {
                        ObjWithSKBase o = mode == "UseSidekickAttribute"
                                                ? new ObjWithSKSimple()
                                                : new ObjWithSKSimpleViaInterface();
                        obs.AllObjects.Should().HaveCount( 1 );
                        o.Message = "Hello!";
                    }
                    else
                    {
                        InternalObjWithSKBase o = mode == "UseSidekickAttribute"
                                                ? new InternalObjWithSKSimple()
                                                : new InternalObjWithSKSimpleViaInterface();
                        obs.AllInternalObjects.Should().HaveCount( 1 );
                        o.Message = "Hello!";
                    }
                } );
                t.Success.Should().BeTrue();
            }
            logs.SingleOrDefault( l => l.Text == "Handling [Hello!]" )
                .Should().NotBeNull();

            if( mode == "UseSidekickAttribute" )
            {
                logs.Any( logs => logs.Text.StartsWith( "Registered: " ) ).Should().BeFalse();
            }
            else
            {
                logs.SingleOrDefault( logs => logs.Text == (type == "ObservableObject"
                                                                    ? "Registered: ObjWithSKSimpleViaInterface."
                                                                    : "Registered: InternalObjWithSKSimpleViaInterface.") )
                    .Should().NotBeNull();
            }

            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    if( type == "ObservableObject" )
                    {
                        ObjWithSKBase another = mode == "UseSidekickAttribute"
                                                            ? (ObjWithSKBase)new ObjWithSKSimple()
                                                            : new ObjWithSKSimpleViaInterface();
                        obs.AllObjects.Should().HaveCount( 2 );
                        var o = (ObjWithSKBase)obs.AllObjects.First();
                        o.Message = "FromO";
                        another.Message = "FromA";
                    }
                    else
                    {
                        InternalObjWithSKBase another = mode == "UseSidekickAttribute"
                                                            ? new InternalObjWithSKSimple()
                                                            : new InternalObjWithSKSimpleViaInterface();
                        obs.AllInternalObjects.Should().HaveCount( 2 );
                        var o = (InternalObjWithSKBase)obs.AllInternalObjects.First();
                        o.Message = "FromO";
                        another.Message = "FromA";
                    }
                } );
                t.Success.Should().BeTrue();
            }
            logs.SingleOrDefault( l => l.Text == "Handling [FromO]" ).Should().NotBeNull();
            logs.SingleOrDefault( l => l.Text == "Handling [FromA]" ).Should().NotBeNull();
            if( mode == "UseSidekickAttribute" )
            {
                logs.Any( logs => logs.Text.StartsWith( "Registered: " ) ).Should().BeFalse();
            }
            else
            {
                logs.SingleOrDefault( logs => logs.Text == (type == "ObservableObject"
                                                                    ? "Registered: ObjWithSKSimpleViaInterface."
                                                                    : "Registered: InternalObjWithSKSimpleViaInterface.") )
                    .Should().NotBeNull();
            }

            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            using( var obs2 = TestHelper.SaveAndLoad( obs ) )
            {
                TransactionResult t = obs2.Modify( TestHelper.Monitor, () =>
                {
                    if( type == "ObservableObject" )
                    {
                        var o = (ObjWithSKBase)obs2.AllObjects.First();
                        var a = (ObjWithSKBase)obs2.AllObjects.Skip( 1 ).First();
                        o.Message = "O!";
                        a.Message = "A!";
                    }
                    else
                    {
                        var o = (InternalObjWithSKBase)obs2.AllInternalObjects.First();
                        var a = (InternalObjWithSKBase)obs2.AllInternalObjects.Skip( 1 ).First();
                        o.Message = "O!";
                        a.Message = "A!";
                    }
                } );
                t.Success.Should().BeTrue();
            }
            obs.IsDisposed.Should().BeTrue( "SaveAndLoad disposed it." );
            logs.SingleOrDefault( l => l.Text == "Handling [O!]" ).Should().NotBeNull();
            logs.SingleOrDefault( l => l.Text == "Handling [A!]" ).Should().NotBeNull();
            if( mode == "UseSidekickAttribute" )
            {
                logs.Any( logs => logs.Text.StartsWith( "Registered: " ) ).Should().BeFalse();
            }
            else
            {
                logs.Where( logs => logs.Text == (type == "ObservableObject"
                                                    ? "Registered: ObjWithSKSimpleViaInterface."
                                                    : "Registered: InternalObjWithSKSimpleViaInterface.") )
                    .Should().HaveCount( 2 );
            }
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

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(sidekick_with_ExternalService) + '_' + mode, startTimer: true, serviceProvider: services );

            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                TransactionResult t = obs.Modify( TestHelper.Monitor, () =>
                {
                    ObjWithSKBase o = mode == "UseSidekickAttribute"
                                            ? (ObjWithSKBase)new ObjWithSKWithDependencies()
                                            : new ObjWithSKWithDependenciesViaInterface();
                    o.Message = "Hello!";
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
