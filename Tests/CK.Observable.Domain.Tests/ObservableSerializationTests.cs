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

        [SerializationVersion( 0 )]
        public class CustomRoot : ObservableRootObject
        {
            public ObservableDictionary<string, CustomImmutable> ImmutablesById { get; set; }
            public ObservableList<string> SomeList { get; set; }

            protected CustomRoot( ObservableDomain domain ) : base( domain )
            {
            }

            protected CustomRoot( IBinaryDeserializerContext d ) : base( d )
            {
                var r = d.StartReading();
                ImmutablesById = (ObservableDictionary<string, CustomImmutable>)r.ReadObject();
                SomeList = (ObservableList<string>)r.ReadObject();
            }

            void Write( BinarySerializer w )
            {
                w.WriteObject( ImmutablesById );
                w.WriteObject( SomeList );
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
    }
}
