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
    /// Base behavior for timed event management.
    /// </summary>
    public abstract class ObservableTimedEventBase : IDisposable
    {
        internal TimerHost TimerHost;
        internal int ActiveIndex;
        internal DateTime ExpectedDueTimeUtc;

        EventHandler<ObservableTimedEventArgs> _handlers;
        int _handlerCount;

        internal ObservableTimedEventBase( DateTime dueTimeUtc )
        {
            if( dueTimeUtc.Kind != DateTimeKind.Utc ) throw new ArgumentException( nameof( dueTimeUtc ), "Must be a Utc DateTime." );
            TimerHost = ObservableDomain.GetCurrentActiveDomain().TimerHost;
            ExpectedDueTimeUtc = dueTimeUtc;
            TimerHost.OnChanged( this );
        }

        /// <summary>
        /// Gets whether this timed event is active.
        /// </summary>
        public bool IsActive => GetIsActive();

        internal abstract bool GetIsActive();

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDisposed => TimerHost == null;

        /// <summary>
        /// Gets or sets an optional name for this timed object.
        /// Default to null.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The timed event.
        /// </summary>
        public event EventHandler<ObservableTimedEventArgs> Elapsed
        {
            add
            {
                if( value == null ) throw new ArgumentNullException( nameof( EventHandler<ObservableTimedEventArgs> ) );
                CheckDisposed();
                _handlers += value;
                ++_handlerCount;
                TimerHost.OnChanged( this );
            }
            remove
            {
                CheckDisposed();
                var hBefore = _handlers;
                _handlers -= value;
                if( !ReferenceEquals( hBefore, _handlers ) )
                {
                    --_handlerCount;
                    TimerHost.OnChanged( this );
                }
            }
        }

        internal void CheckDisposed()
        {
            if( IsDisposed ) throw new ObjectDisposedException( ToString() );
        }

        internal void DoRaise( IActivityMonitor monitor, DateTime current, bool ignoreOnTimerException )
        {
            Debug.Assert( !IsDisposed );
            var h = _handlers;
            if( h != null )
            {
                var ev = new ObservableTimedEventArgs( current, ExpectedDueTimeUtc );
                using( monitor.OpenTrace( $"Raising {ToString()} (Delta: {ev.Delta.TotalMilliseconds} ms)." ) )
                {
                    if( ignoreOnTimerException )
                    {
                        var hList = h.GetInvocationList();
                        for( int i = 0; i < hList.Length; ++i )
                        {
                            h = (EventHandler<ObservableTimedEventArgs>)hList[i];
                            try
                            {
                                h.Invoke( this, ev );
                            }
                            catch( Exception ex )
                            {
                                monitor.Warn( $"While raising {ToString()}. Ignoring error.", ex );
                            }
                        }
                    }
                    else h.Invoke( this, ev );
                }
            }
        }

        internal abstract void OnAfterRaiseUnchanged();

        /// <summary>
        /// Disposes this timed event.
        /// </summary>
        public void Dispose()
        {
            if( !IsDisposed )
            {
                TimerHost.OnChanged( this );
                TimerHost = null;
                _handlers = null;
            }
        }
    }

}
