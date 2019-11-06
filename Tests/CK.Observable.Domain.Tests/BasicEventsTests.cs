using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.ComponentModel;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class BasicEventsTests
    {
        [Test]
        public void simple_property_changed_events()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                Car c0 = null;
                Car c1 = null;
                TransactionResult events;

                events = domain.Modify( TestHelper.Monitor, () =>
                {
                    c0 = new Car( "First Car" );
                    c1 = new Car( "Second Car" );
                } );
                Check( events, "NewObject 0 (Car).",
                               "NewObject 1 (Car).",
                               "NewProperty Name -> 0.",
                               "PropertyChanged 0.Name = First Car.",
                               "NewProperty Speed -> 1.",
                               "PropertyChanged 0.Speed = 0.",
                               "NewProperty Position -> 2.",
                               "PropertyChanged 0.Position = (0,0).",
                               "NewProperty CurrentMechanic -> 3.",
                               "PropertyChanged 0.CurrentMechanic = null.",
                               "PropertyChanged 1.Name = Second Car.",
                               "PropertyChanged 1.Speed = 0.",
                               "PropertyChanged 1.Position = (0,0).",
                               "PropertyChanged 1.CurrentMechanic = null." );

                events = domain.Modify( TestHelper.Monitor, () =>
                {
                    c0.Speed = 1;
                } );
                Check( events, "PropertyChanged 0.Speed = 1." );

                events = domain.Modify( TestHelper.Monitor, () =>
                {
                    c0.Speed = 78;
                    c1.Position = new Position( 1.5, 2.3 );
                } );
                Check( events, "PropertyChanged 0.Speed = 78.", "PropertyChanged 1.Position = (1.5,2.3)." );
            }
        }

        [Test]
        public void property_changed_events_use_the_last_value()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                Car c = null;
                TransactionResult result;

                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    c = new Car( "First Car" );
                } );

                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    c.Position = new Position( 1.0, 2.0 );
                    c.Speed = 1;
                    c.Speed = 2;
                    c.Speed = 3;
                    c.Position = new Position( 3.0, 4.0 );
                } );
                Check( result, "PropertyChanged 0.Speed = 3.", "PropertyChanged 0.Position = (3,4)." );
            }
        }

        [Test]
        public void INotifyPropertyChanged_is_supported_only_because_of_PropertyChanged_Fody_and_NotSupportedException_is_raised()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    InternalCounter counter = new InternalCounter();
                    Car c = new Car( "First Car" );
                    Assert.Throws<NotSupportedException>( () => ((INotifyPropertyChanged)c).PropertyChanged += counter.SilentIncrement );
                } );
            }
        }

        [Test]
        public void SafeEvent_automatically_cleanup_Disposed_targets()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    InternalCounter counter = new InternalCounter();

                    Car c1 = new Car( "First Car" );
                    c1.SpeedChanged += counter.Increment;
                    c1.PositionChanged += counter.SilentIncrement;

                    Car c2 = new Car( "Second Car" );
                    c2.SpeedChanged += counter.Increment;
                    c2.PositionChanged += counter.SilentIncrement;

                    Car c3 = new Car( "Third Car" );
                    c3.SpeedChanged += counter.Increment;
                    c3.PositionChanged += counter.SilentIncrement;

                    c1.Position = new Position( 1.0, 1.0 );
                    c2.Speed = 12;
                    c3.Position = new Position( 1.0, 1.0 );

                    counter.Count.Should().Be( 3 );

                    c2.Dispose();
                    c2.Invoking( c => c.Speed = 78 ).Should().Throw<ObjectDisposedException>();
                    c2.Invoking( c => c.Position = new Position( 10.0, 10.0 ) ).Should().Throw<ObjectDisposedException>();

                    c1.Speed = 42;
                    c3.Position = new Position( 187.0, 1.0 );

                    counter.Count.Should().Be( 5 );

                    c1.Dispose();
                    c3.Speed = 64678;

                    counter.Count.Should().Be( 6 );

                    c3.Dispose();

                    counter.Count.Should().Be( 6 );

                } ).Success.Should().BeTrue();
            }
        }

        [Test]
        public void explicit_property_changed_events_are_automatically_triggered()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                int bang = 0;
                domain.Modify( TestHelper.Monitor, () =>
                {
                    InternalCounter counter = new InternalCounter();

                    Car c = new Car( "First Car" );

                    // The safe event is also subscribed.
                    c.SpeedChanged += counter.Increment;

                    counter.Count.Should().Be( 0 );
                    c.Speed = 56;
                    counter.Count.Should().Be( 1 );
                    c.Speed = 57;
                    counter.Count.Should().Be( 2 );
                    c.Speed = 57;
                    counter.Count.Should().Be( 2, "No change." );

                    EventHandler incBang = ( o, e ) => bang++;
                    c.PositionChanged += incBang;
                    bang.Should().Be( 0 );
                    c.Position = new Position( 1.0, 1.0 );
                    bang.Should().Be( 1 );
                    c.Position = new Position( 1.0, 2.0 );
                    bang.Should().Be( 2 );
                    c.PositionChanged -= incBang;
                    c.Position = new Position( 1.0, 3.0 );
                } );
                bang.Should().Be( 2 );
            }
        }

        [Test]
        public void ObservableList_is_observable_thanks_to_Item_Inserted_Set_RemovedAt_and_CollectionCleared_events()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    Car c = new Car( "First Car" );
                    Garage g = new Garage();
                    using( var gS = g.Cars.Monitor() )
                    {
                        g.Cars.Add( c );
                        gS.Should().Raise( "ItemInserted" )
                            .WithSender( g.Cars )
                            .WithArgs<ListInsertEvent>( ev => ev.Index == 0 && ev.Item == c && ev.Object == g.Cars );
                    }
                    using( var gS = g.Cars.Monitor() )
                    {
                        // Equality check: set is skipped, we detect the no-change.
                        g.Cars[0] = c;
                        gS.Should().NotRaise( "ItemInserted" );
                        gS.Should().NotRaise( "ItemSet" );
                    }
                    using( var gS = g.Cars.Monitor() )
                    {
                        g.Cars[0] = null;
                        gS.Should().Raise( "ItemSet" )
                            .WithSender( g.Cars )
                            .WithArgs<ListSetAtEvent>( ev => ev.Index == 0 && ev.Value == null && ev.Object == g.Cars );
                    }
                    using( var gS = g.Cars.Monitor() )
                    {
                        g.Cars.Remove( null );
                        gS.Should().Raise( "ItemRemovedAt" )
                            .WithSender( g.Cars )
                            .WithArgs<ListRemoveAtEvent>( ev => ev.Index == 0 && ev.Object == g.Cars );
                    }
                    g.Cars.Add( c );
                    g.Cars.Add( null );
                    using( var gS = g.Cars.Monitor() )
                    {
                        g.Cars.Clear();
                        gS.Should().Raise( "CollectionCleared" )
                            .WithSender( g.Cars );
                    }
                    g.Cars.Should().BeEmpty();
                    using( var gS = g.Cars.Monitor() )
                    {
                        g.Cars.Clear();
                        gS.Should().NotRaise( "CollectionCleared" );
                    }

                } ).Should().NotBeNull();
            }
        }

        [Test]
        public void list_changed_events()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                Car c0 = null;
                Car c1 = null;
                Garage g = null;
                TransactionResult result;

                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    c0 = new Car( "C1" );
                    c1 = new Car( "C2" );
                    g = new Garage();
                } );
                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    g.Cars.Add( c0 );
                    g.Cars.Insert( 0, c1 );
                    g.Cars.Clear();
                } );
                Check( result, "ListInsert 4[0] = 'Car C1'.", "ListInsert 4[0] = 'Car C2'.", "CollectionClear 4." );
            }
        }

        [Test]
        public void simple_reflexes()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                Car c = null;
                Mechanic m = null;
                Garage g = null;
                TransactionResult result;
                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    g = new Garage();
                    c = new Car( "C" );
                    m = new Mechanic( g ) { FirstName = "Jon", LastName = "Doe" };
                } );
                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    m.CurrentCar = c;
                } );
                Check( result, "PropertyChanged 4.CurrentMechanic = 'Mechanic Jon Doe'.",
                               "PropertyChanged 5.CurrentCar = 'Car C'." );
                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    m.CurrentCar = null;
                } );
                Check( result, "PropertyChanged 4.CurrentMechanic = null.",
                               "PropertyChanged 5.CurrentCar = null." );

                result = domain.Modify( TestHelper.Monitor, () =>
                {
                    c.CurrentMechanic = m;
                } );
                Check( result, "PropertyChanged 5.CurrentCar = 'Car C'.",
                               "PropertyChanged 4.CurrentMechanic = 'Mechanic Jon Doe'." );

            }
        }

        [Test]
        public void ObservableDictionary_is_observable_thanks_to_Item_Added_Set_Removed_and_CollectionCleared_events()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    var d = new ObservableDictionary<string, int>();
                    using( var dM = d.Monitor() )
                    {
                        d.Add( "One", 1 );
                        dM.Should().Raise( "ItemAdded" )
                            .WithSender( d )
                            .WithArgs<CollectionMapSetEvent>( ev => ev.Key.Equals( "One" )
                                                                    && ev.Value.Equals( 1 )
                                                                    && ev.Object == d );
                    }
                    using( var dM = d.Monitor() )
                    {
                        d["This is added"] = 3712;
                        dM.Should().Raise( "ItemAdded" )
                            .WithSender( d )
                            .WithArgs<CollectionMapSetEvent>( ev => ev.Key.Equals( "This is added" )
                                                                    && ev.Value.Equals( 3712 )
                                                                    && ev.Object == d );
                    }
                    using( var dM = d.Monitor() )
                    {
                        // Equality check: set is skipped.
                        d["One"] = 1;
                        dM.Should().NotRaise( "ItemAdded" );
                        dM.Should().NotRaise( "ItemSet" );
                    }
                    using( var dM = d.Monitor() )
                    {
                        d["One"] = 0;
                        dM.Should().Raise( "ItemSet" )
                            .WithSender( d )
                            .WithArgs<CollectionMapSetEvent>( ev => ev.Key.Equals( "One" )
                                                                    && ev.Value.Equals( 0 )
                                                                    && ev.Object == d );
                    }
                    using( var dM = d.Monitor() )
                    {
                        d.Remove( "One" );
                        dM.Should().Raise( "ItemRemoved" )
                            .WithSender( d )
                            .WithArgs<CollectionRemoveKeyEvent>( ev => ev.Key.Equals( "One" ) && ev.Object == d );
                    }
                    using( var dM = d.Monitor() )
                    {
                        d.Remove( "One" ).Should().BeFalse( "No more 'One' item." );
                        dM.Should().NotRaise( "ItemRemoved" );
                    }
                    d.Add( "Two", 2 );
                    d.Add( "Three", 3 );
                    using( var dM = d.Monitor() )
                    {
                        d.Clear();
                        dM.Should().Raise( "CollectionCleared" ).WithSender( d );
                    }
                    d.Should().BeEmpty();
                    using( var dM = d.Monitor() )
                    {
                        d.Clear();
                        dM.Should().NotRaise( "CollectionCleared" );
                    }

                } ).Should().NotBeNull();
            }
        }

        static void Check( TransactionResult events, params string[] e )
        {
            events.Events.Should().HaveCount( e.Length );
            events.Events.Select( ev => ev.ToString() ).Should().Contain( e );
        }

    }


}
