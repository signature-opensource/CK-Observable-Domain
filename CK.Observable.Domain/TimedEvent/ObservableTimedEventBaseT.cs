using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Base behavior for timed event management that implements the <see cref="Elapsed"/> event.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableTimedEventBase<TEventArgs> : ObservableTimedEventBase where TEventArgs : ObservableTimedEventArgs
    {
        ObservableEventHandler<TEventArgs> _handlers;

        protected ObservableTimedEventBase()
        {
        }

        protected ObservableTimedEventBase( IBinaryDeserializerContext c )
            : base( c )
        {
            var r = c.StartReading();
            _handlers = new ObservableEventHandler<TEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            _handlers.Write( w );
        }

        /// <summary>
        /// Gets whether this timed event is active.
        /// There must be at least one <see cref="Elapsed"/> registered callback for this to be true.
        /// </summary>
        public override bool IsActive => _handlers.HasHandlers && GetIsActive();

        private protected abstract bool GetIsActive();

        private protected abstract TEventArgs ReusableArgs { get; }

        /// <summary>
        /// The timed event.
        /// </summary>
        public event SafeEventHandler<TEventArgs> Elapsed
        {
            add
            {
                this.CheckDisposed();
                _handlers.Add( value, nameof( Elapsed ) );
                TimeManager.OnChanged( this );
            }
            remove
            {
                this.CheckDisposed();
                if( _handlers.Remove( value ) ) TimeManager.OnChanged( this );
            }
        }

        internal override void DoRaise( IActivityMonitor monitor, DateTime current, bool throwException )
        {
            Debug.Assert( !IsDisposed );
            if( _handlers.HasHandlers )
            {
                var ev = ReusableArgs;
                ev.Current = current;
                ev.Expected = ExpectedDueTimeUtc;
                ev.DeltaMilliSeconds = (int)(current - ExpectedDueTimeUtc).TotalMilliseconds;
                using( monitor.OpenDebug( $"Raising {ToString()} (Delta: {ev.DeltaMilliSeconds} ms)." ) )
                {
                    if( throwException ) _handlers.Raise( this, ev );
                    else
                    {
                        try
                        {
                            _handlers.Raise( this, ev );
                        }
                        catch( Exception ex )
                        {
                            monitor.Warn( $"Error while raising Timed event. It is ignored.", ex );
                        }
                    }
                }
            }
        }

        private protected virtual void OnRaising( IActivityMonitor monitor, bool throwException )
        {
        }

        /// <summary>
        /// Disposes this timer.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            if( !IsDisposed ) _handlers.RemoveAll();
        }
    }

}
