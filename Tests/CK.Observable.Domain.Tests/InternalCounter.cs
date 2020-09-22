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

        public TestCounter()
        {
        }

        TestCounter( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading().Reader;
            Count = r.ReadInt32();
        }

        void Write( BinarySerializer w )
        {
            w.Write( Count );
        }

        /// <summary>
        /// This event is automatically raised each time Count changed.
        /// This is a safe event: it is serialized and automatically cleanup of any registered disposed <see cref="IDisposableObject"/>.
        /// </summary>
        public SafeEventHandler<ObservableDomainEventArgs> CountChanged;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SilentIncrement( object sender, EventArgs e ) => Count++;

        public void Increment( object sender, EventMonitoredArgs e )
        {
            Domain.Monitor.Should().BeSameAs( e.Monitor, "InternalObject provides the transaction monitor." );
            Count++;
            e.Monitor.Info( $"Incremented Count = {Count}." );
        }

    }
}
