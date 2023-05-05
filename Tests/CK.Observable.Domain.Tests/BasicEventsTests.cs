using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{

    [TestFixture]
    public class BasicEventsTests
    {
        [Test]
        public async Task simple_property_changed_events_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(simple_property_changed_events_Async), startTimer: true ))
            {
                Car c0 = null!;
                Car c1 = null!;

                IReadOnlyList<ObservableEvent>? events = null;
                domain.TransactionDone += ( d, ev ) => events = ev.Events;

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c0 = new Car( "First Car" );
                    c1 = new Car( "Second Car" );
                } );
                Debug.Assert( events != null );

                Check( events, "NewObject 0 (Car).",
                               "NewObject 1 (Car).",
                               "NewProperty Name -> 0.",
                               "PropertyChanged 0.Name = First Car.",
                               "NewProperty TestSpeed -> 1.",
                               "PropertyChanged 0.TestSpeed = 0.",
                               "NewProperty Position -> 2.",
                               "PropertyChanged 0.Position = (0,0).",
                               "NewProperty Power -> 3.",
                               "PropertyChanged 0.Power = 0.",
                               "NewProperty CurrentMechanic -> 4.",
                               "PropertyChanged 0.CurrentMechanic = null.",
                               "NewProperty IsDestroyed -> 5.",
                               "PropertyChanged 0.IsDestroyed = False.",
                               "NewProperty OId -> 6.",
                               "PropertyChanged 0.OId = 0.",
                               "PropertyChanged 1.Name = Second Car.",
                               "PropertyChanged 1.TestSpeed = 0.",
                               "PropertyChanged 1.Position = (0,0).",
                               "PropertyChanged 1.Power = 0.",
                               "PropertyChanged 1.CurrentMechanic = null.",
                               "PropertyChanged 1.IsDestroyed = False.",
                               "PropertyChanged 1.OId = 1." );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c0.TestSpeed = 1;
                } );
                Check( events, "PropertyChanged 0.TestSpeed = 1." );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c0.TestSpeed = 78;
                    c1.Position = new Position( 1.5, 2.3 );
                } );

                Check( events, "PropertyChanged 0.TestSpeed = 78.", "PropertyChanged 1.Position = (1.5,2.3)." );
            }
        }

        [Test]
        public async Task property_changed_events_use_the_last_value_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(property_changed_events_use_the_last_value_Async), startTimer: true ) )
            {
                IReadOnlyList<ObservableEvent>? events = null;
                domain.TransactionDone += ( d, ev ) => events = ev.Events;

                Car c = null!;

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c = new Car( "First Car" );
                } );
                Debug.Assert( events != null );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c.Position = new Position( 1.0, 2.0 );
                    c.TestSpeed = 1;
                    c.TestSpeed = 2;
                    c.TestSpeed = 3;
                    c.Position = new Position( 3.0, 4.0 );
                } );
                Check( events, "PropertyChanged 0.TestSpeed = 3.", "PropertyChanged 0.Position = (3,4)." );
            }
        }

        [Test]
        public async Task INotifyPropertyChanged_is_supported_only_because_of_PropertyChanged_Fody_and_NotSupportedException_is_raised_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof( INotifyPropertyChanged_is_supported_only_because_of_PropertyChanged_Fody_and_NotSupportedException_is_raised_Async ), startTimer: true ) )
            {
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    TestCounter counter = new TestCounter();
                    Car c = new Car( "First Car" );
                    Assert.Throws<NotSupportedException>( () => ((INotifyPropertyChanged)c).PropertyChanged += (o,e) => { } );
                } );
            }
        }

        [Test]
        public async Task the_way_PropertyChanged_Fody_works_Async()
        {
            using var d = new ObservableDomain(TestHelper.Monitor, nameof(the_way_PropertyChanged_Fody_works_Async), startTimer: true ); 
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                TestCounter counter = new TestCounter();
                var c = new Car( "Fody" );
                c.PositionChanged += counter.IncrementNoLog;
                c.TestSpeedChanged += counter.Increment;
                c.PowerChanged += counter.IncrementNoLog;

                counter.Count.Should().Be( 0 );
                c.TestSpeed = 1;
                counter.Count.Should().Be( 1 );

                c.Position = new Position( 1.0, 1.0 );
                counter.Count.Should().Be( 2 );

                c.Power = 1;
                counter.Count.Should().Be( 3 );

            } );
        }

        [Test]
        public async Task SafeEvent_automatically_cleanup_Disposed_targets_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(SafeEvent_automatically_cleanup_Disposed_targets_Async), startTimer: true ) )
            {
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    TestCounter counter = new TestCounter();

                    Car c1 = new Car( "First Car" );
                    c1.TestSpeedChanged += counter.Increment;
                    c1.PositionChanged += counter.IncrementNoLog;

                    Car c2 = new Car( "Second Car" );
                    c2.TestSpeedChanged += counter.Increment;
                    c2.PositionChanged += counter.IncrementNoLog;

                    Car c3 = new Car( "Third Car" );
                    c3.TestSpeedChanged += counter.Increment;
                    c3.PositionChanged += counter.IncrementNoLog;

                    c1.Position = new Position( 1.0, 1.0 );
                    c2.TestSpeed = 12;
                    c3.Position = new Position( 1.0, 1.0 );

                    counter.Count.Should().Be( 3 );

                    c2.Destroy();
                    c2.Invoking( c => c.TestSpeed = 78 ).Should().Throw<ObjectDestroyedException>();
                    c2.Invoking( c => c.Position = new Position( 10.0, 10.0 ) ).Should().Throw<ObjectDestroyedException>();

                    c1.TestSpeed = 42;
                    c3.Position = new Position( 187.0, 1.0 );

                    counter.Count.Should().Be( 5 );

                    c1.Destroy();
                    c3.TestSpeed = 64678;

                    counter.Count.Should().Be( 6 );

                    c3.Destroy();

                    counter.Count.Should().Be( 6 );

                } );
            }
        }

        static int Bang = 0;
        static void IncBang( object sender ) => Bang++;

        [Test]
        public async Task explicit_property_changed_events_are_automatically_triggered_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(explicit_property_changed_events_are_automatically_triggered_Async), startTimer: true ) )
            {
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    TestCounter counter = new TestCounter();

                    Car c = new Car( "First Car" );

                    // The safe event is also subscribed.
                    c.TestSpeedChanged += counter.Increment;

                    counter.Count.Should().Be( 0 );
                    c.TestSpeed = 56;
                    counter.Count.Should().Be( 1 );
                    c.TestSpeed = 57;
                    counter.Count.Should().Be( 2 );
                    c.TestSpeed = 57;
                    counter.Count.Should().Be( 2, "No change." );

                    Bang = 0;
                    c.PositionChanged += IncBang;
                    Bang.Should().Be( 0 );
                    c.Position = new Position( 1.0, 1.0 );
                    Bang.Should().Be( 1 );
                    c.Position = new Position( 1.0, 2.0 );
                    Bang.Should().Be( 2 );
                    c.PositionChanged -= IncBang;
                    c.Position = new Position( 1.0, 3.0 );
                    Bang.Should().Be( 2, "No change" );
                } );
            }
        }



        [Test]
        public async Task list_changed_events_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(list_changed_events_Async), startTimer: true ) )
            {
                Car c0 = null!;
                Car c1 = null!;
                Garage g = null!;

                IReadOnlyList<ObservableEvent>? events = null;
                domain.TransactionDone += ( d, ev ) => events = ev.Events;

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c0 = new Car( "C1" );
                    c1 = new Car( "C2" );
                    g = new Garage();
                } );
                Debug.Assert( events != null );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    g.Cars.Add( c0 );
                    g.Cars.Insert( 0, c1 );
                    g.Cars.Clear();
                } );

                Check( events, "ListInsert 4[0] = 'Car C1'.", "ListInsert 4[0] = 'Car C2'.", "CollectionClear 4." );
            }
        }

        [Test]
        public async Task simple_reflexes_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(simple_reflexes_Async), startTimer: true ) )
            {
                Car c = null!;
                Mechanic m = null!;
                Garage g = null!;

                IReadOnlyList<ObservableEvent>? events = null;
                domain.TransactionDone += ( d, ev ) => events = ev.Events;

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    g = new Garage();
                    c = new Car( "C" );
                    m = new Mechanic( g ) { FirstName = "Jon", LastName = "Doe" };
                } );
                Debug.Assert( events != null );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    m.CurrentCar = c;
                } );
                Check( events, "PropertyChanged 4.CurrentMechanic = 'Mechanic Jon Doe'.",
                               "PropertyChanged 5.CurrentCar = 'Car C'." );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    m.CurrentCar = null;
                } );
                Check( events, "PropertyChanged 4.CurrentMechanic = null.",
                               "PropertyChanged 5.CurrentCar = null." );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c.CurrentMechanic = m;
                } );
                Check( events, "PropertyChanged 5.CurrentCar = 'Car C'.",
                               "PropertyChanged 4.CurrentMechanic = 'Mechanic Jon Doe'." );

            }
        }

        static List<ObservableEvent> _safeEvents = new List<ObservableEvent>();
        static void CheckLastAndClear<T>( Action<T> match ) where T : ObservableEvent
        {
            match( (T)_safeEvents[_safeEvents.Count - 1] );
            _safeEvents.Clear();
        }
        static void OnSafeEvent( object sender, ObservableEvent e ) => _safeEvents.Add( e );


        [Test]
        public async Task ObservableList_is_observable_thanks_to_Item_Inserted_Set_RemovedAt_and_CollectionCleared_events_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(ObservableList_is_observable_thanks_to_Item_Inserted_Set_RemovedAt_and_CollectionCleared_events_Async), startTimer: true ) )
            {
                IReadOnlyList<ObservableEvent>? events = null;
                domain.TransactionDone += ( d, ev ) => events = ev.Events;

                Garage g = null!;
                Car c = null!;
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    c = new Car( "First Car" );
                    g = new Garage();
                    g.Cars.ItemInserted += OnSafeEvent;
                    g.Cars.CollectionCleared += OnSafeEvent;
                    g.Cars.ItemRemovedAt += OnSafeEvent;
                    g.Cars.ItemSet += OnSafeEvent;
                } );
                Debug.Assert( events != null );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => g.Cars.Add( c ) );
                Check( events, "ListInsert 3[0] = 'Car First Car'." );
                CheckLastAndClear<ListInsertEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.ListInsert );
                    e.Index.Should().Be( 0 );
                    e.Object.Should().BeSameAs( g.Cars );
                    e.Item.Should().BeSameAs( c );
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    g.Cars.Add( c );
                    g.Cars.Add( c );
                } );
                Check( events, "ListInsert 3[1] = 'Car First Car'.", "ListInsert 3[2] = 'Car First Car'." );
                CheckLastAndClear<ListInsertEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.ListInsert );
                    e.Index.Should().Be( 2 );
                    e.Object.Should().BeSameAs( g.Cars );
                    e.Item.Should().BeSameAs( c );
                } );

                // Equality check: set is skipped, we detect the no-change.
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    g.Cars[0] = c;
                    g.Cars[1] = c;
                    g.Cars[2] = c;
                } );
                Check( events );
                _safeEvents.Should().BeEmpty();

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => g.Cars[0] = null! );
                Check( events, "ListSetAt 3[0] = null." );
                CheckLastAndClear<ListSetAtEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.ListSetAt );
                    e.Index.Should().Be( 0 );
                    e.Object.Should().BeSameAs( g.Cars );
                    e.Value.Should().BeNull();
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => g.Cars.Remove( null! ) );
                Check( events, "ListRemoveAt 3[0]." );
                CheckLastAndClear<ListRemoveAtEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.ListRemoveAt );
                    e.Index.Should().Be( 0 );
                    e.Object.Should().BeSameAs( g.Cars );
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => g.Cars.Clear() );
                Check( events, "CollectionClear 3." );
                CheckLastAndClear<CollectionClearEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.CollectionClear );
                    e.Object.Should().BeSameAs( g.Cars );
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => g.Cars.Clear() );
                Check( events );
                _safeEvents.Should().BeEmpty();
            }
        }

        [Test]
        public async Task ObservableDictionary_is_observable_thanks_to_Item_Added_Set_Removed_and_CollectionCleared_events_Async()
        {
            using( var domain = new ObservableDomain(TestHelper.Monitor, nameof(ObservableDictionary_is_observable_thanks_to_Item_Added_Set_Removed_and_CollectionCleared_events_Async), startTimer: true ) )
            {
                IReadOnlyList<ObservableEvent>? events = null;
                domain.TransactionDone += ( d, ev ) => events = ev.Events;

                ObservableDictionary<string, int> dick = null!;
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    dick = new ObservableDictionary<string, int>();
                    dick.ItemAdded += OnSafeEvent;
                    dick.ItemRemoved += OnSafeEvent;
                    dick.ItemSet += OnSafeEvent;
                    dick.CollectionCleared += OnSafeEvent;
                });
                Debug.Assert( events != null );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick.Add( "One", 1 ) );
                Check( events, "CollectionMapSet 0[One] = 1" );
                CheckLastAndClear<CollectionMapSetEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.CollectionMapSet );
                    e.Key.Should().Be( "One" );
                    e.Object.Should().BeSameAs( dick );
                    e.Value.Should().Be( 1 );
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick["This is added"] = 3712 );
                Check( events, "CollectionMapSet 0[This is added] = 3712" );
                CheckLastAndClear<CollectionMapSetEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.CollectionMapSet );
                    e.Key.Should().Be( "This is added" );
                    e.Object.Should().BeSameAs( dick );
                    e.Value.Should().Be( 3712 );
                } );

                // Equality check: set is skipped.
                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick["One"] = 1 );
                Check( events );
                _safeEvents.Should().BeEmpty();

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick["One"] = 0 );
                Check( events, "CollectionMapSet 0[One] = 0" );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick.Remove( "One" ) );
                Check( events, "CollectionRemoveKey 0[One]" );
                CheckLastAndClear<CollectionRemoveKeyEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.CollectionRemoveKey );
                    e.Key.Should().Be( "One" );
                    e.Object.Should().BeSameAs( dick );
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick.Remove( "One" ).Should().BeFalse( "No more 'One' item." ) );
                Check( events );
                _safeEvents.Should().BeEmpty();

                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    dick.Add( "Two", 2 );
                    dick.Add( "Three", 3 );
                } );
                await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    dick.Clear();
                    dick.Should().BeEmpty();
                } );
                Check( events, "CollectionClear 0." );
                CheckLastAndClear<CollectionClearEvent>( e =>
                {
                    e.EventType.Should().Be( ObservableEventType.CollectionClear );
                    e.Object.Should().BeSameAs( dick );
                } );

                await domain.ModifyThrowAsync( TestHelper.Monitor, () => dick.Clear() );
                Check( events );
                _safeEvents.Should().BeEmpty();
            }
        }

        static void Check( IReadOnlyList<ObservableEvent> events, params string[] e )
        {
            events.Select( ev => ev.ToString() ).Should().BeEquivalentTo( e );
        }

    }


}
