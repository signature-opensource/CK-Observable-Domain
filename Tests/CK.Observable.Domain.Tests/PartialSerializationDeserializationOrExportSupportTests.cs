using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class PartialSerializationDeserializationOrExportSupportTests
    {
        public class ExportableOnly : ObservableObject
        {
            public string Name { get; set; }
        }

        [Test]
        public void exportable_but_not_serializable()
        {
            var d = new ObservableDomain();
            d.Modify( () =>
            {
                new ExportableOnly() { Name = "Albert" };
            } );
            var export = d.ExportToString();
            export.Should().Be( @"{""N"":1,""C"":1,""P"":[""Name""],""O"":[{""þ"":[0,""A""]},{""°"":1,""Name"":""Albert""}],""R"":[]}" );
            d.Invoking( sut => sut.Save( new MemoryStream() ) )
                .Should().Throw<InvalidOperationException>().WithMessage( "*is not serializable*" );
        }

        [NotExportable]
        [SerializationVersion(0)]
        public class SerializableOnly : ObservableObject
        {
            public SerializableOnly()
            {
            }

            public SerializableOnly( BinaryDeserializer d )
                : base( d )
            {
                var r = d.StartReading();
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
            var d = new ObservableDomain();
            d.Modify( () =>
            {
                new SerializableOnly() { Name = "Albert" };
            } );
            var d2 = DomainSerializationTests.SaveAndLoad( d );
            d2.AllObjects.OfType<SerializableOnly>().Single().Name.Should().Be( "Albert" );

            d.Invoking( sut => sut.ExportToString() )
                .Should().Throw<InvalidOperationException>().WithMessage( "*is not exportable*" ); 
        }

    }
}
