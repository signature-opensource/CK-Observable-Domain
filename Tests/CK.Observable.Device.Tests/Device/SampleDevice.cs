using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Core.Impl;
using CK.DeviceModel;
using CK.PerfectEvent;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal.Execution;

namespace CK.Observable.Device.Tests
{
    public class SampleDevice : Device<SampleDeviceConfiguration>
    {
        Task _run;
        CancellationTokenSource? _stopToken;

        string _messagePrefix;
        int _period;
        int _count;
        PerfectEventSender<SampleDevice, string> _messageChanged;
        int _syncCommandCount;
        int _asyncCommandCount;

        public SampleDevice( IActivityMonitor monitor, SampleDeviceConfiguration c )
            : base( monitor, c )
        {
            _run = Task.CompletedTask;
            _period = c.PeriodMilliseconds;
            _messagePrefix = c.Message;
            _messageChanged = new PerfectEventSender<SampleDevice, string>();
        }

        /// <summary>
        /// The device exposes <see cref="PerfectEvent{TEvent}"/> or <see cref="PerfectEvent{TSender, TArg}"/>.
        /// These events support synchronous as well as asynchronous handlers.
        /// </summary>
        public PerfectEvent<SampleDevice, string> MessageChanged => _messageChanged.PerfectEvent;

        /// <summary>
        /// This kind of state exposition is dangerous as soon as more than one datum is available and access
        /// to them is made outside any event (like <see cref="MessageChanged"/>): even is writing/reading each
        /// of them is atomic (which is the case for basic value types - not too big like references, integersn etc.),
        /// accessing two or more data is at risk.
        /// Here, the message may not contain the same count as the <see cref="DangerousCurrentCount"/> property.
        /// </summary>
        public string? DangerousCurrentMessage { get; private set; }

        /// <summary>
        /// Gets the current count. See <see cref="DangerousCurrentMessage"/>.
        /// </summary>
        public int DangerousCurrentCount => _count;

        /// <summary>
        /// Instead of exposing multiple properties and if a state must be exposed, a single
        /// state class can be implemented that groups the data. 
        /// </summary>
        public class SafeDeviceState
        {
            internal SafeDeviceState( SampleDevice d, string message )
            {
                DangerousCurrentMessage = message;
                DangerousCurrentCount = d._count;
                SyncCommandCount = d._syncCommandCount;
                AsyncCommandCount = d._asyncCommandCount;
            }

            public string DangerousCurrentMessage { get; }

            public int DangerousCurrentCount { get; }

            public int SyncCommandCount { get; }

            public int AsyncCommandCount { get; }

        }

        /// <summary>
        /// The current state that, as a class instance, supports atomic access:
        /// it is replaced as a whole.
        /// <para>
        /// Accessing this state should de done through a local variable that captures
        /// this reference.
        /// </para>
        /// </summary>
        public SafeDeviceState? State { get; private set; }

        /// <summary>
        /// Standard asynchronous loop start.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reason">The reason to start.</param>
        /// <returns>True on success, false on error.</returns>
        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            _stopToken = new CancellationTokenSource();
            _run = RunLoop();
            return Task.FromResult( true );
        }

        async Task RunLoop()
        {
            Debug.Assert( _stopToken != null );
            _count = 0;
            var monitor = new ActivityMonitor( "SampleDevice Loop." );
            for( ; ; )
            {
                await Task.Delay( _period ).ConfigureAwait( false );
                // Check and break the forever loop make the stop more reactive than the clasical while( !_stopToken.IsCancellationRequested ) { ... }.
                if( _stopToken.IsCancellationRequested )
                {
                    monitor.Debug( "StopToken signaled." );
                    break;
                }
                monitor.Debug( "Running." );
                // This is where exposed message/count can be incoherent.
                var message = $"{_messagePrefix} - {_count++}";
                DangerousCurrentMessage = message;
                // Setting the reference to the new state object is atomic.
                State = new SafeDeviceState( this, message );
                // Raising the event: note that as long as the properties like
                // DangerousCurrentMessage/Count are read DURING the handling of
                // such events, everything is safe!
                // However, we should never trust the developper to NOT access such properties
                // outside of an event context: such dangerous state exposure should be forbidden.
                await _messageChanged.SafeRaiseAsync( monitor, this, message ).ConfigureAwait( false );
            }
            State = null;
            monitor.MonitorEnd();
        }

        protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            Debug.Assert( _stopToken != null );
            _stopToken.Cancel();
            return _run;
        }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, SampleDeviceConfiguration config )
        {
            if( _period != config.PeriodMilliseconds )
            {
                _period = config.PeriodMilliseconds;
                _messagePrefix = config.Message;
                return Task.FromResult( DeviceReconfiguredResult.UpdateSucceeded );
            }
            return Task.FromResult( DeviceReconfiguredResult.None );
        }

        /// <summary>
        /// Caution: Handling command is done out of any lock: this may be called concurrently.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to handle.</param>
        protected override void DoHandleCommand( IActivityMonitor monitor, SyncDeviceCommand command )
        {
            if( command is SampleSyncCommand )
            {
                Interlocked.Increment( ref _syncCommandCount );
                return;
            }
            // The base implementation throws.
            base.DoHandleCommand( monitor, command );
        }

        /// <summary>
        /// Caution: Handling command is done out of any lock: this may be called concurrently.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to handle.</param>
        protected override Task DoHandleCommandAsync( IActivityMonitor monitor, AsyncDeviceCommand command )
        {
            if( command is SampleAsyncCommand )
            {
                Interlocked.Increment( ref _asyncCommandCount );
                return Task.CompletedTask;
            }
            return base.DoHandleCommandAsync( monitor, command );
        }

        /// <summary>
        /// This must clenap the device's resources.
        /// Typically events should be cleared (thanks to <c>RemoveAll()</c>).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>the awaitable.</returns>
        protected override Task DoDestroyAsync( IActivityMonitor monitor )
        {
            _messageChanged.RemoveAll();
            return Task.CompletedTask;
        }
    }
}
