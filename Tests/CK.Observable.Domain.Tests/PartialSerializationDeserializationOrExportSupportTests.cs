using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class PartialSerializationDeserializationOrExportSupportTests
    {

        [NotExportable]
        [SerializationVersion( 0 )]
        public class SerializableOnly : ObservableObject
        {
            public SerializableOnly()
            {
            }

            SerializableOnly( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
            {
                Name = r.ReadNullableString();
            }

            void Write( BinarySerializer s )
            {
                s.WriteNullableString( Name );
            }

            public string Name { get; set; }
        }

        [Test]
        public void serializable_but_not_exportable()
        {
            using var d = new ObservableDomain(TestHelper.Monitor, nameof(serializable_but_not_exportable), startTimer: true );
            d.Modify( TestHelper.Monitor, () =>
            {
                new SerializableOnly() { Name = "Albert" };
            } );
            using( var d2 = TestHelper.SaveAndLoad( d ) )
            {
                d2.AllObjects.OfType<SerializableOnly>().Single().Name.Should().Be( "Albert" );

                d2.Invoking( sut => sut.ExportToString() )
                    .Should().Throw<InvalidOperationException>().WithMessage( "*is not exportable*" );
            }
            d.IsDisposed.Should().BeTrue( "SaveAndLoad disposed it." );
        }

    }
}
