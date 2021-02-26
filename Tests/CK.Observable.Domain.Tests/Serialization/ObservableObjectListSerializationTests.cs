using FluentAssertions;
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

            C( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
            {
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

            protected O( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
            {
            }

            void Write( BinarySerializer w )
            {
            }
        }


        [Test]
        public void auto_reference_of_container()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
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
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
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
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                long c0Id = 0, c1Id = 0;
                C originalC0 = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c0 = originalC0 = new C();
                    var c1 = new C();
                    c0Id = c0.OId.UniqueId;
                    c1Id = c1.OId.UniqueId;
                    if( sameOrder ) c0.ObjectList.Add( c1 );
                    else c1.ObjectList.Add( c0 );
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

                var c0 = (C)d.AllObjects[c0Id];
                c0.Should().NotBeSameAs( originalC0 );

                var c1 = (C)d.AllObjects[c1Id];
                if( sameOrder )
                {
                    c0.ObjectList[0].Should().BeSameAs( c1 );
                }
                else
                {
                    c1.ObjectList[0].Should().BeSameAs( c0 );
                }
            }
        }


        [Test]
        public void Self_registering_ObservableObject_can_be_serialized()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c = new C();
                    var o = new C();
                    o.Destroyed += c.OnObjectDisposed;
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }

    }
}
