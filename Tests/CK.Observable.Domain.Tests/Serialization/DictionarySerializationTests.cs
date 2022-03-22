using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using CK.Core;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Serialization
{
    [TestFixture]
    public class DictionarySerializationTests
    {
        [Test]
        public void standard_serialization()
        {
            var d = new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase );
            d.Add( "A", 1 );
            d["a"].Should().Be( 1 );

            var d2 = (Dictionary<string, int>)TestHelper.SaveAndLoadViaStandardSerialization( d );
            d2["a"].Should().Be( 1 );
            d2.Add( "B", 2 );
            d2["b"].Should().Be( 2 );

            d2.Comparer.Should().NotBeSameAs( StringComparer.InvariantCultureIgnoreCase );

            //
            BinarySerializer.IdempotenceCheck( d, new SimpleServiceContainer() );

        }


        [Test]
        public void basic_types_dictionary_serialization()
        {
            var int2String = new Dictionary<int,string>
            {
                { 12, "Twelve" },
                { 11, "Eleven" },
                { 10, "Ten" },
                { 9, "Nine" },
                { 8, "Eight" }
            };
            object back = TestHelper.SaveAndLoadObject( int2String );
            back.Should().BeAssignableTo<Dictionary<int, string>>();
            var b = (Dictionary<int, string>)back;
            b.Should().BeEquivalentTo( int2String );

            //
            BinarySerializer.IdempotenceCheck( int2String, new SimpleServiceContainer() );
        }

        [Test]
        public void dictionary_with_comparer_serialization()
        {
            var string2Int = new Dictionary<string,int>( StringComparer.InvariantCultureIgnoreCase )
            {
                { "Twelve", 12 },
                { "Eleven", 11 },
                { "Ten", 10 },
                { "Nine", 9 },
                { "Eight", 8 }
            };
            object back = TestHelper.SaveAndLoadObject( string2Int );
            back.Should().BeAssignableTo<Dictionary<string,int>>();
            var b = (Dictionary<string, int>)back;
            b.Should().BeEquivalentTo( string2Int );
            b["TWELVE"].Should().Be( 12 );
            //
            BinarySerializer.IdempotenceCheck( string2Int, new SimpleServiceContainer() );
        }

        [Test]
        public void array_of_dictionaries()
        {
            var array = new[]
            {
                new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase )
                {
                    { "One", 1 },
                    { "Two", 2 }
                },
                new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase )
                {
                    { "Twelve", 12 },
                    { "Eleven", 11 },
                    { "Ten", 10 },
                    { "Nine", 9 },
                    { "Eight", 8 }
                }
            };
            object back = TestHelper.SaveAndLoadObject( array );
            back.Should().BeAssignableTo < Dictionary<string, int>[]>();
            var b = (Dictionary<string, int>[])back;
            b.Should().BeEquivalentTo( array );
            b[0]["TWO"].Should().Be( 2 );
            b[1]["TWELVE"].Should().Be( 12 );
            //
            BinarySerializer.IdempotenceCheck( array, new SimpleServiceContainer() );
        }

        [Test]
        public void array_of_dictionaries_with_references()
        {
            var array = new Dictionary<string, int>[5];
            array[0] = new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase )
                {
                    { "One", 1 },
                    { "Two", 2 }
                };
            array[1] = array[0];
            array[2] = new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase )
                {
                    { "Twelve", 12 },
                    { "Eleven", 11 },
                    { "Ten", 10 },
                    { "Nine", 9 },
                    { "Eight", 8 }
                };
            array[3] = null;
            array[4] = array[2];

            object back = TestHelper.SaveAndLoadObject( array );
            back.Should().BeAssignableTo < Dictionary<string, int>[]>();
            var b = (Dictionary<string, int>[])back;
            b.Should().BeEquivalentTo( array );
            b[0]["TWO"].Should().Be( 2 );
            b[1].Should().BeSameAs( b[0] );
            b[2]["TWELVE"].Should().Be( 12 );
            b[3].Should().BeNull();
            b[4].Should().BeSameAs( b[2] );

            BinarySerializer.IdempotenceCheck( array, new SimpleServiceContainer() );
        }

    }
}
