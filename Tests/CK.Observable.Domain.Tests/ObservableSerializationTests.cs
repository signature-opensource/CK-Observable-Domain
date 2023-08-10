using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ObservableSerializationTests
    {
        [TestCase( "Person" )]
        [TestCase( "SuspendableClock" )]
        [TestCase( "Timer" )]
        [TestCase( "Reminder" )]
        [TestCase( "AutoCounter" )]
        public async Task one_object_serialization_Async( string type )
        {
            using var handler = TestHelper.CreateDomainHandler( $"{nameof( one_object_serialization_Async )}-{type}", startTimer: false, serviceProvider: null );

            object o = null!;
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                switch( type )
                {
                    case "Person": o = new Sample.Person() { FirstName = "XX", LastName = "YY", Age = 35 }; break;
                    case "SuspendableClock": o = new SuspendableClock(); break;
                    case "Timer": o = new ObservableTimer( DateTime.UtcNow.AddDays( 1 ) ); break;
                    case "Reminder": o = new ObservableReminder( DateTime.UtcNow.AddDays( 1 ) ); break;
                    case "AutoCounter": o = new TimedEvents.StupidAutoCounter( 10000 ); break;
                }

            } );

            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: type != "SuspendableClock" );

            handler.Domain.Read( TestHelper.Monitor, () =>
            {
                if( o is ObservableObject )
                {
                    handler.Domain.AllObjects.Items.Should().HaveCount( 1 );
                }
                else if( o is ObservableTimer )
                {
                    handler.Domain.TimeManager.Timers.Should().HaveCount( 1 );
                }
                else if( o is ObservableReminder )
                {
                    handler.Domain.TimeManager.Reminders.Should().HaveCount( 1 );
                }
                else if( o is InternalObject )
                {
                    handler.Domain.AllInternalObjects.Should().HaveCount( 1 );
                }
            } );

            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: type != "SuspendableClock" );
        }

        [Test]
        public async Task simple_idempotence_checks_Async()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( simple_idempotence_checks_Async ), startTimer: true ) )
            {
                TestHelper.Monitor.Info( "Test 1" );
                await d.ModifyThrowAsync( TestHelper.Monitor, () => new Sample.Car( "Zoé" ) );
                d.AllObjects.Items.Should().HaveCount( 1 );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

                TestHelper.Monitor.Info( "Test 2" );
                await d.ModifyThrowAsync( TestHelper.Monitor, () => d.AllObjects.Items.Single().Destroy() );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

                TestHelper.Monitor.Info( "Test 3" );
                await d.ModifyThrowAsync( TestHelper.Monitor, () => new Sample.Car( "Zoé is back!" ) );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }


        [SerializationVersion(0)]
        class ObservableWithJaggedArrays : ObservableObject
        {
            public ObservableWithJaggedArrays()
            {
            }

            ObservableWithJaggedArrays( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                Cars = r.ReadObject<Car[][]>();
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in ObservableWithJaggedArrays o )
            {
                w.WriteObject( o.Cars );
            }

            public Car[][] Cars { get; set; }
        }

        [Test]
        public async Task jagged_arrays_of_ObservableObjects_idempotence_checks_Async()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( jagged_arrays_of_ObservableObjects_idempotence_checks_Async ), startTimer: false ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var ketru = new ObservableWithJaggedArrays();
                    ketru.Cars = new[]
                    {
                        new[] { new Car( "Zoé" ), new Car( "Xrq" ) },
                        new[] { new Car( "O1" ), new Car( "O2" ), new Car( "O3" ) }
                    };
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var ketru = d.AllObjects.Items.OfType<ObservableWithJaggedArrays>().Single();
                    ketru.Cars[0].Should().HaveCount( 2 );
                    ketru.Cars[1].Should().HaveCount( 3 );
                    ketru.Cars[1][2].Name.Should().Be( "O3" );
                } );

            }
        }


        [Test]
        public async Task immutable_string_serialization_test_Async()
        {
            using( var od = new ObservableDomain<CustomRoot>( TestHelper.Monitor, nameof( immutable_string_serialization_test_Async ), startTimer: false) )
            {
                await od.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    od.Root.ImmutablesById = new ObservableDictionary<string, CustomImmutable>();

                    var myImmutable = new CustomImmutable( "ABC000", "My object" );
                    od.Root.ImmutablesById.Add( myImmutable.Id, myImmutable );
                    od.Root.SomeList = new ObservableList<string>();
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );
            }
        }

        [Test]
        public async Task created_then_disposed_event_test_Async()
        {
            using( var od = new ObservableDomain<CustomRoot>( TestHelper.Monitor, nameof( created_then_disposed_event_test_Async ), startTimer: true ) )
            {
                // Prepare initial state
                await od.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    od.Root.CustomObservableList = new ObservableList<CustomObservable>();
                } );

                var initialState = od.ExportToString();

                // Add some events for good measure
                await od.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    // Create Observable and immutables
                    var myImmutable = new CustomImmutable( "ABC000", "My object" );
                    var myCustomObservable = new CustomObservable();

                    // Set Immutable in Dictionary of Observable
                    myCustomObservable.ImmutablesById.Add( myImmutable.Id, myImmutable );

                    // Add observable to List of Root
                    od.Root.CustomObservableList.Add( myCustomObservable );

                    // Destroy Dictionary of Observable
                    myCustomObservable.ImmutablesById.Destroy();

                    // Destroy Observable
                    myCustomObservable.Destroy();

                    // Remove Observable from List of Root
                    bool removed = od.Root.CustomObservableList.Remove( myCustomObservable );
                    removed.Should().BeTrue();
                } );
            }
        }

        [TestCase( true )]
        [TestCase( false )]
        public async Task IdempotenceSerializationCheck_works_on_disposing_Observables_Async( bool alwaysDisposeChild )
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( IdempotenceSerializationCheck_works_on_disposing_Observables_Async ), startTimer: true ) )
            {
                TestDisposableObservableObject oldObject = null;
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    oldObject = new TestDisposableObservableObject( alwaysDisposeChild );
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

                oldObject.Should().NotBeNull();
                oldObject.IsDestroyed.Should().BeTrue( "The reference of the object was disposed when the domain was reloaded" );
                oldObject.ChildObject.IsDestroyed.Should().BeTrue( "The reference of the object's ObservableObject child was disposed when the domain was reloaded" );
            }
        }



        [SerializationVersion( 0 )]
        public class CustomRoot : ObservableRootObject
        {
            public ObservableDictionary<string, CustomImmutable> ImmutablesById { get; set; }
            public ObservableList<string> SomeList { get; set; }
            public ObservableList<CustomObservable>? CustomObservableList { get; set; }

            public CustomRoot() { }

            CustomRoot( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                ImmutablesById = r.ReadObject<ObservableDictionary<string, CustomImmutable>>();
                SomeList = r.ReadObject<ObservableList<string>>();
                CustomObservableList = r.ReadNullableObject<ObservableList<CustomObservable>>();
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in CustomRoot o )
            {
                w.WriteObject( o.ImmutablesById );
                w.WriteObject( o.SomeList );
                w.WriteNullableObject( o.CustomObservableList );
            }
        }

        [SerializationVersion( 0 )]
        public class CustomImmutable : BinarySerialization.ICKSlicedSerializable
        {
            public string Id { get; }
            public string Title { get; }

            public CustomImmutable( string id, string title )
            {
                Id = id;
                Title = title;
            }

            CustomImmutable( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo? info )
            {
                Id = r.Reader.ReadNullableString();
                Title = r.Reader.ReadNullableString();
            }


            public static void Write( BinarySerialization.IBinarySerializer w, in CustomImmutable o )
            {
                w.Writer.WriteNullableString( o.Id );
                w.Writer.WriteNullableString( o.Title );
            }

        }

        [SerializationVersion( 0 )]
        public class CustomObservable : ObservableObject
        {
            public ObservableDictionary<string, CustomImmutable> ImmutablesById { get; }

            public CustomObservable()
            {
                ImmutablesById = new ObservableDictionary<string, CustomImmutable>();
            }

            protected CustomObservable( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
            {
                ImmutablesById = r.ReadObject<ObservableDictionary<string, CustomImmutable>>();
            }


            public static void Write( BinarySerialization.IBinarySerializer w, in CustomObservable o )
            {
                w.WriteObject( o.ImmutablesById );
            }

        }

        [SerializationVersion( 0 )]
        public class TestDisposableObservableObject : ObservableObject
        {
            public bool AlwaysDisposeChild { get; }
            public ObservableList<int> ChildObject { get; }

            public TestDisposableObservableObject( bool alwaysDisposeChild )
            {
                AlwaysDisposeChild = alwaysDisposeChild;
                ChildObject = new ObservableList<int>();
            }

            TestDisposableObservableObject( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
            {
                AlwaysDisposeChild = r.Reader.ReadBoolean();
                ChildObject = r.ReadObject<ObservableList<int>>();
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in TestDisposableObservableObject o )
            {
                w.Writer.Write( o.AlwaysDisposeChild );
                w.WriteObject( o.ChildObject );
            }

            protected override void OnDestroy()
            {
                ChildObject.Destroy();
                base.OnDestroy();
            }

            protected override void OnUnload()
            {
                if( AlwaysDisposeChild )
                {
                    ChildObject.Destroy();
                }
            }
        }

        [SerializationVersion( 0 )]
        public class ReminderAndTimerBag : InternalObject
        {
            readonly List<ObservableReminder> _reminders;
            readonly ObservableList<ObservableReminder> _oReminders;
            readonly List<ObservableTimer> _timers;
            readonly ObservableList<ObservableTimer> _oTimers;
            readonly int _identifier;

            public ReminderAndTimerBag( int id )
            {
                _identifier = id;
                _reminders = new List<ObservableReminder>();
                _oReminders = new ObservableList<ObservableReminder>();
                _timers = new List<ObservableTimer>();
                _oTimers = new ObservableList<ObservableTimer>();
            }

            ReminderAndTimerBag( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
            {
                _identifier = r.Reader.ReadInt32();
                _reminders = r.ReadObject<List<ObservableReminder>>();
                _oReminders = r.ReadObject<ObservableList<ObservableReminder>>();
                _timers = r.ReadObject<List<ObservableTimer>>();
                _oTimers = r.ReadObject<ObservableList<ObservableTimer>>();
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in ReminderAndTimerBag o )
            {
                w.Writer.Write( o._identifier );
                w.WriteObject( o._reminders );
                w.WriteObject( o._oReminders );
                w.WriteObject( o._timers );
                w.WriteObject( o._oTimers );
            }

            public void Create( int count = 5 )
            {
                for( int i = 0; i < count; ++i )
                {
                    var r1 = new ObservableReminder( DateTime.UtcNow.AddDays( 1 ) );
                    _reminders.Add( r1 );
                    var r2 = new ObservableReminder( DateTime.UtcNow.AddDays( 1 ) );
                    _oReminders.Add( r2 );
                    var t1 = new ObservableTimer( DateTime.UtcNow.AddDays( 1 ) ) { Name = $"LTimer n°{i}" };
                    _timers.Add( t1 );
                    var t2 = new ObservableTimer( DateTime.UtcNow.AddDays( 1 ) ) { Name = $"OTimer n°{i}" };
                    _oTimers.Add( t2 );
                }
            }
        }

        [Test]
        public async Task lot_of_timed_events_test_Async()
        {
            using( var od = new ObservableDomain( TestHelper.Monitor, nameof( lot_of_timed_events_test_Async ), startTimer: true ) )
            {
                await od.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    new ReminderAndTimerBag( 1 ).Create( 1 );

                } );
                od.AllInternalObjects.Should().HaveCount( 1 );
                od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 4 );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );

                await od.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    new ReminderAndTimerBag( 2 ).Create( 20 );

                } );
                od.AllInternalObjects.Should().HaveCount( 2 );
                od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 4 * 21 );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );
            }
        }


    }
}
