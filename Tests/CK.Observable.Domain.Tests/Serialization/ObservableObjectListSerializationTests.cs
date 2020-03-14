using NUnit.Framework;
using System.Collections.Generic;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Serialization
{
    [TestFixture]
    public class ObservableObjectListSerializationTests
    {
        [Test]
        public void Self_registering_ObservableObject_can_be_serialized()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var objectContainer = new TestObjectContainer();
                    var selfRegisteringObject = new SelfRegisteringObject( new[] { objectContainer } );

                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            }
        }

        [SerializationVersion( 0 )]
        public class SelfRegisteringObject : ObservableObject
        {
            private ObservableList<object> Containers { get; }

            public SelfRegisteringObject( IReadOnlyCollection<TestObjectContainer> testObjectContainers )
            {
                Containers = new ObservableList<object>();
                Containers.AddRange( testObjectContainers );
                foreach( var testObjectContainer in testObjectContainers )
                {
                    testObjectContainer.AddObject( this );
                }
            }

            protected SelfRegisteringObject( IBinaryDeserializerContext d ) : base( d )
            {
                var r = d.StartReading();
                Containers = (ObservableList<object>)r.ReadObject();
            }

            private void Write( BinarySerializer w )
            {
                w.WriteObject( Containers );
            }

            protected override void Dispose( bool shouldCleanup )
            {
                if( shouldCleanup )
                {
                    Containers.Dispose();
                }
                base.Dispose( shouldCleanup );
            }
        }

        [SerializationVersion( 0 )]
        public class TestObjectContainer : ObservableObject
        {
            public ObservableList<SelfRegisteringObject> ObjectList { get; }

            public TestObjectContainer()
            {
                ObjectList = new ObservableList<SelfRegisteringObject>();
            }

            protected TestObjectContainer( IBinaryDeserializerContext d ) : base( d )
            {
                var r = d.StartReading();
                ObjectList = (ObservableList<SelfRegisteringObject>)r.ReadObject();
            }

            private void Write( BinarySerializer bs )
            {
                bs.WriteObject( ObjectList );
            }

            protected override void Dispose( bool shouldCleanup )
            {
                if( shouldCleanup )
                {
                    ObjectList.Dispose();
                }
                base.Dispose( shouldCleanup );
            }

            public void AddObject( SelfRegisteringObject selfRegisteringObject )
            {
                if( !ObjectList.Contains( selfRegisteringObject ) )
                {
                    selfRegisteringObject.Disposed += TestObject_Disposed;
                    ObjectList.Add( selfRegisteringObject );
                }
            }

            private void TestObject_Disposed( object sender, ObservableDomainEventArgs e )
            {
                if( sender is SelfRegisteringObject testObject )
                {
                    ObjectList.Remove( testObject );
                }
            }
        }
    }
}
