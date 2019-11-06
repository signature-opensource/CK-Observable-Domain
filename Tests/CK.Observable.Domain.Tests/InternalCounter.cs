using CK.Core;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Domain.Tests
{

    [SerializationVersion( 0 )]
    public sealed class InternalCounter : InternalObject
    {
        public int Count { get; private set; }

        public InternalCounter()
        {
        }

        InternalCounter( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            Count = r.ReadInt32();
        }

        void Write( BinarySerializer w )
        {
            w.Write( Count );
        }

        public void SilentIncrement( object sender, EventArgs e ) => Count++;

        public void Increment( object sender, EventMonitoredArgs e )
        {
            Monitor.Should().BeSameAs( e.Monitor, "InternalObject provides the transaction monitor." );
            Count++;
            e.Monitor.Info( $"Incremented Count = {Count}." );
        }

    }
}
