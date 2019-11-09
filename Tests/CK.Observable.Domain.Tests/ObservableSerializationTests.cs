using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.IO;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ObservableSerializationTests
    {
        [SerializationVersion(0)]
        class ForgetToCallBaseDeserializationCtor : InternalObject
        {
            public ForgetToCallBaseDeserializationCtor()
            {
            }

            protected ForgetToCallBaseDeserializationCtor( IBinaryDeserializerContext c )
                // : base( c )
            {
                var r = c.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }

        [SerializationVersion(0)]
        class ForgetToCallBaseDeserializationCtorSpecialized : ForgetToCallBaseDeserializationCtor
        {
            public ForgetToCallBaseDeserializationCtorSpecialized()
            {
            }

            ForgetToCallBaseDeserializationCtorSpecialized( IBinaryDeserializerContext c )
                : base( c )
            {
                var r = c.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }

        [TestCase("")]
        [TestCase("debugMode")]
        public void forgetting_to_call_base_deserialization_ctor_throws_explicit_InvalidDataException(string mode)
        {
            string msgNoDebugMode = $"Missing \": base( c )\" call in deserialization constructor of '{typeof( ForgetToCallBaseDeserializationCtor ).AssemblyQualifiedName}'.";

            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( forgetting_to_call_base_deserialization_ctor_throws_explicit_InvalidDataException ) ) )
            {
                d.Modify( TestHelper.Monitor, () => new ForgetToCallBaseDeserializationCtor() );
                d.AllInternalObjects.Should().HaveCount( 1 );

                d.Invoking( x => TestHelper.SaveAndLoad( d, debugMode: mode == "debugMode" ) ).Should()
                      .Throw<InvalidDataException>()
                      .WithMessage( msgNoDebugMode );
            }

            string msgDebugModeForSpecialized = $"Read string failure: expected string 'After: CK.Observable.InternalObject*";
            string msgForSpecialized = mode == "debugMode" ? msgDebugModeForSpecialized : msgNoDebugMode;

            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( forgetting_to_call_base_deserialization_ctor_throws_explicit_InvalidDataException ) ) )
            {
                d.Modify( TestHelper.Monitor, () => new ForgetToCallBaseDeserializationCtorSpecialized() );
                d.AllInternalObjects.Should().HaveCount( 1 );

                d.Invoking( x => TestHelper.SaveAndLoad( d, debugMode: mode == "debugMode" ) ).Should()
                      .Throw<InvalidDataException>()
                      .WithMessage( msgForSpecialized );
            }
        }

        [Test]
        public void simple_idempotence_checks()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( simple_idempotence_checks ) ) )
            {
                d.Modify( TestHelper.Monitor, () => new Sample.Car( "Zoé" ) );
                d.AllObjects.Should().HaveCount( 1 );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

                d.Modify( TestHelper.Monitor, () => d.AllObjects.Single().Dispose() );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

                d.Modify( TestHelper.Monitor, () => new Sample.Car( "Zoé is back!" ) );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }


        [Test]
        public void immutable_string_serialization_test()
        {
            using( var od = new ObservableDomain<CustomRoot>( TestHelper.Monitor, nameof( immutable_string_serialization_test ) ) )
            {
                od.Modify( TestHelper.Monitor, () =>
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
        public void created_then_disposed_event_test()
        {
            using( var od = new ObservableDomain<CustomRoot>( TestHelper.Monitor, nameof( created_then_disposed_event_test ) ) )
            {
                // Prepare initial state
                od.Modify( TestHelper.Monitor, () =>
                {
                    od.Root.CustomObservableList = new ObservableList<CustomObservable>();
                } );

                var initialState = od.ExportToString();

                // Add some events for good measure
                var events = od.Modify( TestHelper.Monitor, () =>
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
