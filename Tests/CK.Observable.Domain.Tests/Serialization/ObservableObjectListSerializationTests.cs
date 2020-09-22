using NUnit.Framework;
using System.Collections.Generic;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Serialization
{
    [TestFixture]
    public class ObservableObjectListSerializationTests
    {
        [SerializationVersion( 0 )]
        public class C : ObservableObject
        {
            public ObservableList<ObservableObject> ObjectList { get; }

            public C()
            {
                ObjectList = new ObservableList<ObservableObject>();
            }

            protected C( IBinaryDeserializerContext d )
                : base( d )
            {
                var r = d.StartReading().Reader;
                ObjectList = (ObservableList<ObservableObject>)r.ReadObject();
            }

            private void Write( BinarySerializer w )
            {
                w.WriteObject( ObjectList );
            }

            public void OnObjectDisposed( object sender, ObservableDomainEventArgs e ) => ObjectList.Remove( (ObservableObject)sender );
        }

        [SerializationVersion( 0 )]
        public class O : ObservableObject
        {
            public O()
            {
            }

            protected O( IBinaryDeserializerContext d )
                : base( d )
            {
                var r = d.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }


        [Test]
        public void auto_reference_of_container()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c0 = new C();
                    c0.ObjectList.Add( c0 );
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }

        [Test]
        public void auto_reference_of_list()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c0 = new ObservableList<ObservableObject>();
                    c0.Add( c0 );
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }


        [TestCase( true )]
        [TestCase( false )]
        public void cross_reference_of_containers( bool sameOrder )
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c0 = new C();
                    var c1 = new C();
                    if( sameOrder ) c0.ObjectList.Add( c1 );
                    else c1.ObjectList.Add( c0 );
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }


        [Test]
        public void Self_registering_ObservableObject_can_be_serialized()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c = new C();
                    var o = new C();
                    o.Disposed += c.OnObjectDisposed;
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }

    }
}
