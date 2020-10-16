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
        /// Gets the count. Can be called even if <see cref="IDisposableObject.IsDisposed"/> is true.
        /// </summary>
        public int Count { get; private set; }

        ObservableEventHandler _privateTestSerialization;

        public TestCounter()
        {
            // This registers a private method as the event handler.
            _privateTestSerialization.Add( PrivateHandler, "PrivateEvent" );
        }

        protected TestCounter( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading().Reader;
            Count = r.ReadInt32();
            _privateTestSerialization = new ObservableEventHandler( r );
            _privateTestSerialization.HasHandlers.Should().BeTrue();
        }

        void Write( BinarySerializer w )
        {
            w.Write( Count );
            _privateTestSerialization.Write( w );
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
