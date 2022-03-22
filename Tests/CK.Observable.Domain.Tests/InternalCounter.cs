using CK.Core;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Domain.Tests
{

    /// <summary>
    /// This is a sample <see cref="InternalObject"/>.
    /// </summary>
    [SerializationVersion( 0 )]
    public sealed class TestCounter : InternalObject
    {
        /// <summary>
        /// Gets the count. Can be called even if <see cref="IDestroyable.IsDestroyed"/> is true.
        /// </summary>
        public int Count { get; private set; }

        ObservableEventHandler _privateTestSerialization;

        public TestCounter()
        {
            // This registers a private method as the event handler.
            _privateTestSerialization.Add( PrivateHandler, "PrivateEvent" );
        }

        TestCounter( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
        {
            Count = d.Reader.ReadInt32();
            _privateTestSerialization = new ObservableEventHandler( d );
            _privateTestSerialization.HasHandlers.Should().BeTrue();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in TestCounter o )
        {
            s.Writer.Write( o.Count );
            o._privateTestSerialization.Write( s );
        }

        public void IncrementNoLog( object sender ) => Count++;

        public void Increment( object sender, EventMonitoredArgs e )
        {
            Domain.Monitor.Should().BeSameAs( e.Monitor, "InternalObject provides the transaction monitor." );
            Count++;
            e.Monitor.Info( $"Incremented Count = {Count}." );
        }

        void PrivateHandler( object sender )
        {
            // This is to test the serialization of a private handler.
        }

    }

}
