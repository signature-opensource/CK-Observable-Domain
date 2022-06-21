using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class InternalObjectTests
    {
        [SerializationVersion( 0 )]
        public sealed class Invisible : InternalObject
        {
            public Invisible( int noWay )
            {
                NoWay = noWay;
                Message = "I'm Invisible!";
            }

            Invisible( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                Message = d.Reader.ReadString();
                NoWay = d.Reader.ReadInt32();
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in Invisible o )
            {
                s.Writer.Write( o.Message );
                s.Writer.Write( o.NoWay );
            }

            public int NoWay { get; set; }

            public string Message { get; set; }
        }

        [SerializationVersion( 0 )]
        public sealed class Visible : ObservableObject
        {
            public Visible( int yes )
            {
                Yes = yes;
                Message = "I'm Visible!";
            }

            Visible( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                NotVisible = d.ReadNullableObject<Invisible>();
                Yes = d.Reader.ReadInt32();
                Message = d.Reader.ReadNullableString();
            }

            public static void Write( BinarySerialization.IBinarySerializer s, in Visible o )
            {
                s.WriteNullableObject( o.NotVisible );
                s.Writer.Write( o.Yes );
                s.Writer.WriteNullableString( o.Message );
            }

            public Invisible? NotVisible { get; set; }

            public int Yes { get; set; }

            public string? Message { get; set; }

        }


        [Test]
        public async Task internal_objects_should_not_be_exported_Async()
        {
            using var domain = new ObservableDomain(TestHelper.Monitor, nameof(internal_objects_should_not_be_exported_Async), startTimer: true );
            var collector = new JsonEventCollector( domain );

            // The first event is always empty.
            await domain.ModifyThrowAsync( TestHelper.Monitor, null );
            collector.LastEvent.ExportedEvents.Should().BeEmpty();

            // Let's do it now.
            Visible? v = null;
            await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var i = new Invisible( 666 );
                v = new Visible( 3712 );
                v.NotVisible = i;

            } );
            Debug.Assert( v != null );

            // Note that "NotVisible" is the property name: it is exported as the property name. This is
            // because even a non exported property is available on PropertyChanged events, and
            // our ObservablePropertyChangedEventArgs uses the PropertyId. 
            collector.LastEvent.ExportedEvents.Should().ContainAll( "Visible", "Yes", "NotVisible" );
            collector.LastEvent.ExportedEvents.Should().NotContainAny( "Invisible", "NoWay" );

            // This check that the "NotVisible" property is not 'changed': even if its name and identifier
            // has been marshalled, the property of the object itself is not seen by the client. 
            await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var i2 = new Invisible( 2 * 666 );
                v.NotVisible = i2;

            } );
            collector.LastEvent.ExportedEvents.Should().BeEmpty();

        }
    }
}
