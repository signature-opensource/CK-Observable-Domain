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
    /// Base class for timed event management that implements the <see cref="Elapsed"/> event.
    /// <see cref="ObservableTimer"/> and <see cref="ObservableReminder"/> are concrete specializations of this base class.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableTimedEventBase<TEventArgs> : ObservableTimedEventBase where TEventArgs : ObservableTimedEventArgs
    {
        ObservableEventHandler<TEventArgs> _handlers;

        /// <summary>
        /// Initializes a new <see cref="ObservableTimedEventBase{TEventArgs}"/>.
        /// </summary>
        protected ObservableTimedEventBase()
        {
        }

        protected ObservableTimedEventBase( RevertSerialization _ ) : base( _ ) { }

        ObservableTimedEventBase( IBinaryDeserializer r, TypeReadInfo? info )
            : base( RevertSerialization.Default )
        {
            _handlers = new ObservableEventHandler<TEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            _handlers.Write( w );
        }

        internal override bool HasHandlers => _handlers.HasHandlers;

        /// <summary>
        /// Gets whether this timed event is active.
        /// There must be at least one <see cref="Elapsed"/> registered callback for this to be true. and if a
        /// bound <see cref="ObservableTimedEventBase.SuspendableClock"/> exists, it must be active.
        /// </summary>
        public override bool IsActive => (SuspendableClock == null || SuspendableClock.IsActive)
                                            && _handlers.HasHandlers
                                            && ExpectedDueTimeUtc != Util.UtcMinValue && ExpectedDueTimeUtc != Util.UtcMaxValue
                                            && GetIsActiveFlag();

        /// <summary>
        /// This must compute whether this timed event is logically active.
        /// Always true at this level. ObservableTimers override this to return their <see cref="ObservableTimer.IsActive"/> mutable flag.
        /// </summary>
        /// <returns>True if this timed event is logically active.</returns>
        private protected virtual bool GetIsActiveFlag() => true;

        /// <summary>
        /// This must provide the typed reusable event argument.
        /// </summary>
        private protected abstract TEventArgs ReusableArgs { get; }

        /// <summary>
        /// The timed event.
        /// </summary>
        public event SafeEventHandler<TEventArgs> Elapsed
        {
            add
            {
                this.CheckDestroyed();
                _handlers.Add( value, nameof( Elapsed ) );
                TimeManager.OnChanged( this );
            }
            remove
            {
                this.CheckDestroyed();
                if( _handlers.Remove( value ) ) TimeManager.OnChanged( this );
            }
        }

        internal override void DoRaise( IActivityMonitor monitor, DateTime current, bool throwException )
        {
            Debug.Assert( !IsDestroyed );
            if( _handlers.HasHandlers )
            {
                var ev = ReusableArgs;
                ev.Current = current;
                ev.Expected = ExpectedDueTimeUtc;
                ev.DeltaMilliSeconds = (int)(current - ExpectedDueTimeUtc).TotalMilliseconds;
                using( monitor.OpenDebug( $"Raising {ToString()} (Delta: {ev.DeltaMilliSeconds} ms)." ) )
                {
                    OnRaising( monitor, ev.DeltaMilliSeconds, throwException );
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

        private protected virtual void OnRaising( IActivityMonitor monitor, int deltaMilliSeconds, bool throwException )
        {
        }

        private protected void ClearHandlesAndTag()
        {
            _handlers.RemoveAll();
            Tag = null;
        }

        /// <summary>
        /// Destroys this timed event.
        /// </summary>
        public override void Destroy()
        {
            // Clearing the handlers first makes this timed event logically inactive
            // if it was active.
            if( !IsDestroyed )
            {
                _handlers.RemoveAll();
                base.Destroy();
            }
        }
    }

}
