using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

            SerializableOnly( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                Name = r.Reader.ReadNullableString();
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in SerializableOnly o )
            {
                s.Writer.WriteNullableString( o.Name );
            }

            public string Name { get; set; }
        }

        [Test]
        public async Task serializable_but_not_exportable_Async()
        {
            var d = new ObservableDomain(TestHelper.Monitor, nameof(serializable_but_not_exportable_Async), startTimer: true );
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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
