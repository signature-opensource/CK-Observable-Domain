using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public partial class SidekickTests
    {
        public readonly struct DeserializationInfoValue
        {
            public readonly TimeSpan InactiveDelay;
            public readonly bool IsRollback { get; }
            public readonly bool IsSafeRollback { get; }
            public readonly bool IsDangerousRollback { get; }

            public DeserializationInfoValue( IObservableDomainSidekickManager.IDeserializationInfo i )
            {
                InactiveDelay = i.InactiveDelay;
                IsRollback = i.IsRollback;
                IsSafeRollback = i.IsSafeRollback;
                IsDangerousRollback = i.IsDangerousRollback;
            }
        }

        public class CmdSimple { public string? Text { get; set; } }

        public class SKSimple : ObservableDomainSidekick
        {
            DeserializationInfoValue? _ctorValue;

            public SKSimple( IActivityMonitor monitor, IObservableDomainSidekickManager manager )
                : base( manager )
            {
                if( manager.DeserializationInfo == null )
                {
                    monitor.Info( $"Instantiating Sidekick normally." );
                }
                else
                {
                    monitor.Info( $"Instantiating Sidekick after deserialization." );
                    _ctorValue = new DeserializationInfoValue( manager.DeserializationInfo );
                }
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
                var oS = (ISKSimpleObservableOrInternalObject)o;
                oS.SidekickIsHere = true;
                if( Manager.DeserializationInfo != null )
                {
                    monitor.Info( $"Registered: {o.GetType().Name} after deserialization." );
                    Debug.Assert( _ctorValue is not null );
                    _ctorValue.Value.Should().BeEquivalentTo( Manager.DeserializationInfo );
                    oS.DeserializationInfo = _ctorValue;
                }
                else
                {
                    monitor.Info( $"Registered: {o.GetType().Name} while running sidekick." );
                }
            }

            protected override void OnUnload( IActivityMonitor monitor )
            {
            }
        }

        /// <summary>
        /// Common interface to all observable and internal objects manage by the SKSimple sidekick.
        /// This enables tracking of the deserialization info that is available to the sidekick
        /// from its constructor and RegisterClientObject method.
        /// </summary>
        interface ISKSimpleObservableOrInternalObject
        {
            /// <summary>
            /// This is set by the sidekick (when ISidekickClientObject<SKSimple> is used).
            /// When null, the object has been created after the last deserialization.
            /// When not null, the object has been created by the last deserialization and
            /// this captures the deserialization info.
            /// (There is no point to expose this info on the objects, this is just for the test!)
            /// </summary>
            DeserializationInfoValue? DeserializationInfo { get; set; }

            /// <summary>
            /// This is logged by the CmdSimple command.
            /// </summary>
            string CommandMessage { get; set; }

            /// <summary>
            /// With [UseSidekick] attribute, this is always false since [UseSidekick] doesn't
            /// trigger the call to <see cref="ObservableDomainSidekick.RegisterClientObject(IActivityMonitor, IDestroyable)"/>.
            /// </summary>
            bool SidekickIsHere { get; set; }
        }

        [SerializationVersion( 0 )]
        public class ObjWithSKBase : ObservableObject, ISKSimpleObservableOrInternalObject
        {
            public ObjWithSKBase()
            {
                CommandMessage = "";
            }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            protected ObjWithSKBase( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618 

            ObjWithSKBase( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                CommandMessage = r.Reader.ReadString();
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKBase o )
            {
                s.Writer.Write( o.CommandMessage );
            }

            public bool SidekickIsHere { get; set; }

            public DeserializationInfoValue? DeserializationInfo { get; set; }

            public string CommandMessage { get; set; }

            // Here we use Fody weaving.
            void OnCommandMessageChanged( object before, object after )
            {
                if( Domain.CurrentTransactionStatus.IsRegular() )
                {
                    Domain.SendCommand( new CmdSimple() { Text = CommandMessage } );
                }
            }

        }

        /// <summary>
        /// When using the attribute, the object is not registered onto the sidekick.
        /// If the sidekick needs to know some of the objects of the domain, it has to discover them
        /// by other means.
        /// </summary>
        [UseSidekick( typeof( SKSimple ) )]
        [SerializationVersion( 0 )]
        public class ObjWithSKSimpleAttr : ObjWithSKBase
        {
            public ObjWithSKSimpleAttr()
            {
                Domain.EnsureSidekicks();
            }

            ObjWithSKSimpleAttr( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKSimpleAttr o )
            {
            }
        }

        /// <summary>
        /// When using the ISidekickClientObject<>, the object is automatically registered on the sidekick.
        /// </summary>
        [SerializationVersion( 0 )]
        public class ObjWithSKSimple : ObjWithSKBase, ISidekickClientObject<SKSimple>
        {
            public ObjWithSKSimple()
            {
                // If the transaction is not a regular one (it is a deserialization since it cannot be
                // a Disposing since this object would have been newed in an Unload!), the EnsureSidekicks
                // is ignored.
                Domain.EnsureSidekicks();
                if( Domain.CurrentTransactionStatus.IsRegular() )
                {
                    SidekickIsHere.Should().BeTrue();   
                }
                else
                {
                    SidekickIsHere.Should().BeFalse();
                }
            }

            ObjWithSKSimple( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in ObjWithSKSimple o )
            {
            }
        }

        #region Same 2 objects as above but Internal rather than Observable.
        [SerializationVersion( 0 )]
        public class InternalObjWithSKBase : InternalObject, ISKSimpleObservableOrInternalObject
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

            public DeserializationInfoValue? DeserializationInfo { get; set; }

            public bool SidekickIsHere { get; set; }

            public string CommandMessage
            {
                get => _message;
                set
                {
                    if( _message != value )
                    {
                        _message = value;
                        // This doesn't use FodyWeaving and OnCommandMessageChanged hook.
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
        public class InternalObjWithSKSimpleAttr : InternalObjWithSKBase
        {
            public InternalObjWithSKSimpleAttr()
            {
                Domain.EnsureSidekicks();
            }

            InternalObjWithSKSimpleAttr( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in InternalObjWithSKSimpleAttr o )
            {
            }
        }

        /// <summary>
        /// When using the ISidekickClientObject<>, the object is automatically registered on the sidekick.
        /// </summary>
        [SerializationVersion( 0 )]
        public class InternalObjWithSKSimple : InternalObjWithSKBase, ISidekickClientObject<SKSimple>
        {
            public InternalObjWithSKSimple()
            {
                // If the transaction is not a regular one (it is a deserialization since it cannot be
                // a Disposing since this object would have been newed in an Unload!), the EnsureSidekicks
                // is ignored.
                Domain.EnsureSidekicks();
                if( Domain.CurrentTransactionStatus.IsRegular() )
                {
                    SidekickIsHere.Should().BeTrue();
                }
                else
                {
                    SidekickIsHere.Should().BeFalse();
                }
            }

            InternalObjWithSKSimple( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in InternalObjWithSKSimple o )
            {
            }
        }
        #endregion

        [TestCase( "UseSidekickAttribute", "ObservableObject" )]
        [TestCase( "ISidekickClientObject<>", "ObservableObject" )]
        [TestCase( "UseSidekickAttribute", "InternalObject" )]
        [TestCase( "ISidekickClientObject<>", "InternalObject" )]
        public async Task sidekick_simple_instantiation_and_serialization_Async( string mode, string type )
        {
            // Will be disposed by TestHelper.SaveAndLoad at the end of this test.
            var obs = new ObservableDomain( TestHelper.Monitor, nameof( sidekick_simple_instantiation_and_serialization_Async ) + '-' + mode, startTimer: true );

            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null!;
            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    ISKSimpleObservableOrInternalObject o;
                    if( type == "ObservableObject" )
                    {
                        o = mode == "UseSidekickAttribute" ? new ObjWithSKSimpleAttr() : new ObjWithSKSimple();
                        obs.AllObjects.Should().HaveCount( 1 );
                    }
                    else
                    {
                        o = mode == "UseSidekickAttribute" ? new InternalObjWithSKSimpleAttr() : new InternalObjWithSKSimple();
                        obs.AllInternalObjects.Should().HaveCount( 1 );
                    }
                    o.CommandMessage = "Hello!";
                } );
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
                                                                    ? "Registered: ObjWithSKSimple while running sidekick."
                                                                    : "Registered: InternalObjWithSKSimple while running sidekick.") )
                    .Should().NotBeNull();
            }

            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            {
                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    if( type == "ObservableObject" )
                    {
                        ObjWithSKBase another = mode == "UseSidekickAttribute"
                                                            ? new ObjWithSKSimpleAttr()
                                                            : new ObjWithSKSimple();
                        obs.AllObjects.Should().HaveCount( 2 );
                        var o = (ObjWithSKBase)obs.AllObjects.First();
                        o.CommandMessage = "FromO";
                        another.CommandMessage = "FromA";
                    }
                    else
                    {
                        InternalObjWithSKBase another = mode == "UseSidekickAttribute"
                                                            ? new InternalObjWithSKSimpleAttr()
                                                            : new InternalObjWithSKSimple();
                        obs.AllInternalObjects.Should().HaveCount( 2 );
                        var o = (InternalObjWithSKBase)obs.AllInternalObjects.First();
                        o.CommandMessage = "FromO";
                        another.CommandMessage = "FromA";
                    }
                } );
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
                                                                    ? "Registered: ObjWithSKSimple while running sidekick."
                                                                    : "Registered: InternalObjWithSKSimple while running sidekick.") )
                    .Should().NotBeNull();
            }

            using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
            using( var obs2 = TestHelper.CloneDomain( obs ) )
            {
                await obs2.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    if( type == "ObservableObject" )
                    {
                        var o = (ObjWithSKBase)obs2.AllObjects.First();
                        var a = (ObjWithSKBase)obs2.AllObjects.Skip( 1 ).First();
                        o.CommandMessage = "O!";
                        a.CommandMessage = "A!";
                    }
                    else
                    {
                        var o = (InternalObjWithSKBase)obs2.AllInternalObjects.First();
                        var a = (InternalObjWithSKBase)obs2.AllInternalObjects.Skip( 1 ).First();
                        o.CommandMessage = "O!";
                        a.CommandMessage = "A!";
                    }
                } );
            }
            obs.IsDisposed.Should().BeTrue( "SaveAndLoad disposed the original domain." );

            logs.SingleOrDefault( l => l.Text == "Handling [O!]" ).Should().NotBeNull();
            logs.SingleOrDefault( l => l.Text == "Handling [A!]" ).Should().NotBeNull();
            if( mode == "UseSidekickAttribute" )
            {
                logs.Any( logs => logs.Text.StartsWith( "Registered: " ) ).Should().BeFalse();
            }
            else
            {
                logs.Where( logs => logs.Text == (type == "ObservableObject"
                                                    ? "Registered: ObjWithSKSimple after deserialization."
                                                    : "Registered: InternalObjWithSKSimple after deserialization.") )
                    .Should().HaveCount( 2 );
            }
        }


        [Test]
        public async Task sidekick_DeserializationInfo_Status_and_InactiveDelay_Async()
        {
            var rollbacker = new Clients.ConcreteMemoryTransactionProviderClient();
            var dInitial = new ObservableDomain( TestHelper.Monitor, nameof( sidekick_DeserializationInfo_Status_and_InactiveDelay_Async ), startTimer: false, rollbacker );

            await dInitial.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var oI = new InternalObjWithSKSimple();
                var oO = new ObjWithSKSimple();
                // Since Domain.EnsureSidekicks() is called from their constructors, sidekick instantiation
                // has been done.
                oI.SidekickIsHere.Should().BeTrue();
                oO.SidekickIsHere.Should().BeTrue();

            } );

        }

    }

}
