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
        internal TimeManager TimeManager;
        internal int ActiveIndex;
        internal DateTime ExpectedDueTimeUtc;
        internal ObservableTimedEventBase Next;
        internal ObservableTimedEventBase Prev;

        EventHandler<ObservableTimedEventArgs> _handlers;
        int _handlerCount;

        internal ObservableTimedEventBase()
        {
            TimeManager = ObservableDomain.GetCurrentActiveDomain().TimeManager;
            TimeManager.OnCreated( this );
        }

        /// <summary>
        /// Gets whether this timed event is active.
        /// </summary>
        public bool IsActive => GetIsActive();

        internal abstract bool GetIsActive();

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDisposed => TimeManager == null;

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
                TimeManager.OnChanged( this );
            }
            remove
            {
                CheckDisposed();
                var hBefore = _handlers;
                _handlers -= value;
                if( !ReferenceEquals( hBefore, _handlers ) )
                {
                    --_handlerCount;
                    TimeManager.OnChanged( this );
                }
            }
        }

        internal void CheckDisposed()
        {
            if( IsDisposed ) throw new ObjectDisposedException( ToString() );
        }

        internal void DoRaise( IActivityMonitor monitor, DateTime current, bool throwException )
        {
            Debug.Assert( !IsDisposed );
            var h = _handlers;
            if( h != null )
            {
                var ev = new ObservableTimedEventArgs( current, ExpectedDueTimeUtc );
                using( monitor.OpenTrace( $"Raising {ToString()} (Delta: {ev.Delta.TotalMilliseconds} ms)." ) )
                {
                    if( throwException ) h.Invoke( this, ev );
                    else
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
                }
            }
        }

        internal abstract void OnAfterRaiseUnchanged();

        /// <summary>
        /// This applies to reminders.
        /// </summary>
        internal virtual void ForwardExpectedDueTime( IActivityMonitor monitor, DateTime forwarded )
        {
            monitor.Warn( $"{ToString()}: next due time '{ExpectedDueTimeUtc.ToString( "o" )}' has been forwarded to '{forwarded.ToString( "o" )}'." );
            ExpectedDueTimeUtc = forwarded;
        }

        /// <summary>
        /// Raised when this object is <see cref="Dispose"/>d.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.Load"/>, this event is not
        /// triggered.
        /// </summary>
        public event EventHandler Disposed;


        /// <summary>
        /// Disposes this timed event.
        /// </summary>
        public void Dispose()
        {
            if( !IsDisposed )
            {
                Disposed?.Invoke( this, EventArgs.Empty );
                Disposed = null;
                TimeManager.OnDisposed( this );
                TimeManager = null;
                _handlers = null;
            }
        }
    }

}
