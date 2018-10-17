using FluentAssertions;
using NUnit.Framework;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ObservableSerializationTests
    {
        [Test]
        public void immutable_string_serialization_test()
        {
            var od = new ObservableDomain<CustomRoot>();

            od.Modify( () =>
            {
                od.Root.ImmutablesById = new ObservableDictionary<string, CustomImmutable>();

                var myImmutable = new CustomImmutable( "ABC000", "My object" );
                od.Root.ImmutablesById.Add( myImmutable.Id, myImmutable );


                od.Root.SomeList = new ObservableList<string>();
            } );
            ObservableRootTests.SaveAndLoad( od );
        }

        [Test]
        public void created_then_disposed_event_test()
        {
            var od = new ObservableDomain<CustomRoot>();

            // Prepare initial state
            od.Modify( () =>
            {
                od.Root.CustomObservableList = new ObservableList<CustomObservable>();
            } );

            var initialState = od.ExportToString();

            // Add some events for good measure
            var events = od.Modify( () =>
            {
                // Create Observable and Immutables
                var myImmutable = new CustomImmutable( "ABC000", "My object" );
                var myCustomObservable = new CustomObservable();

                // Set Immutable in Dictionary of Observable
                myCustomObservable.ImmutablesById.Add( myImmutable.Id, myImmutable );

                // Add observable to List of Root
                od.Root.CustomObservableList.Add( myCustomObservable );

                // Destroy Dictionary of Observable
                myCustomObservable.ImmutablesById.Dispose();

                // Destroy Observable
                myCustomObservable.Dispose();

                // Remove Observable from List of Root
                bool removed = od.Root.CustomObservableList.Remove( myCustomObservable );
                removed.Should().BeTrue();
            } );

        }

        [SerializationVersion( 0 )]
        public class CustomRoot : ObservableRootObject
        {
            public ObservableDictionary<string, CustomImmutable> ImmutablesById { get; set; }
            public ObservableList<string> SomeList { get; set; }
            public ObservableList<CustomObservable> CustomObservableList { get; set; }

            protected CustomRoot( ObservableDomain domain ) : base( domain )
            {
            }

            protected CustomRoot( IBinaryDeserializerContext d ) : base( d )
            {
                var r = d.StartReading();
                ImmutablesById = (ObservableDictionary<string, CustomImmutable>)r.ReadObject();
                SomeList = (ObservableList<string>)r.ReadObject();
                CustomObservableList = (ObservableList<CustomObservable>)r.ReadObject();
            }

            void Write( BinarySerializer w )
            {
                w.WriteObject( ImmutablesById );
                w.WriteObject( SomeList );
                w.WriteObject( CustomObservableList );
            }
        }

        [SerializationVersion( 0 )]
        public class CustomImmutable
        {
            public string Id { get; }
            public string Title { get; }

            public CustomImmutable( string id, string title )
            {
                Id = id;
                Title = title;
            }

            protected CustomImmutable( IBinaryDeserializerContext d )
            {
                var r = d.StartReading();
                Id = r.ReadNullableString();
                Title = r.ReadNullableString();
            }


            void Write( BinarySerializer w )
            {
                w.WriteNullableString( Id );
                w.WriteNullableString( Title );
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

            protected CustomObservable( IBinaryDeserializerContext d )
            {
                var r = d.StartReading();
                ImmutablesById = (ObservableDictionary<string, CustomImmutable>)r.ReadObject();
            }


            void Write( BinarySerializer w )
            {
                w.WriteObject( ImmutablesById );
            }

        }
    }
}
