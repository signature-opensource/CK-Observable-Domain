using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ObservableDomainPostActionExecutor
    {
        readonly Channel<TransactionResult> _queue;
        readonly ObservableDomain _domain;
        object? _runSignal;
        bool _stopped;

        public ObservableDomainPostActionExecutor( ObservableDomain d )
        {
            _domain = d;
            _queue = Channel.CreateUnbounded<TransactionResult>( new UnboundedChannelOptions() { SingleReader = true } );
        }

        internal void Enqueue( TransactionResult r )
        {
            if( _runSignal == null )
            {
                _runSignal = new object();
                _ = RunAsync();
            }
            _queue.Writer.TryWrite( r );
        }

        internal bool Stop()
        {
            if( _runSignal != null )
            {
                // Uses the EmptySuccess as the stop signal.
                return _queue.Writer.TryWrite( TransactionResult.EmptySuccess );
            }
            return false;
        }

        internal void WaitStopped()
        {
            Debug.Assert( _runSignal != null );
            lock( _runSignal )
                while( !_stopped )
                    System.Threading.Monitor.Wait( _runSignal );
        }

        internal async Task RunAsync()
        {
            Debug.Assert( _runSignal != null );
            IActivityMonitor monitor = new ActivityMonitor( $"DomainPostAction executor for '{_domain.DomainName}'." ); ;
            var r = _queue.Reader;
            try
            {
                for(; ; )
                {
                    TransactionResult t = await r.ReadAsync().ConfigureAwait( false );
                    if( t == TransactionResult.EmptySuccess )
                    {
                        break;
                    }
                    var actions = await t.DomainActions.ConfigureAwait( false );
                    if( actions == null )
                    {
                        monitor.Debug( $"Skipped domain '{_domain.DomainName}' transaction n°{t.TransactionNumber} DomainPostActions." );
                    }
                    else
                    {
                        var ctx = new PostActionContext( monitor, actions, t );
                        try
                        {
                            var result = await ctx.ExecuteAsync( throwException: false, name: $"domain '{_domain.DomainName}' transaction n°{t.TransactionNumber} DomainPostActions" )
                                                  .ConfigureAwait( false );
                            t.SetDomainPostActionsResult( result );
                        }
                        finally
                        {
                            await ctx.DisposeAsync().ConfigureAwait( false );
                        }
                    }
                }
            }
            catch( Exception ex )
            {
                monitor.Fatal( "Unexpected error in DomainPostAction executor.", ex );
            }
            monitor.MonitorEnd( $"Stopping DomainPostAction executor for '{_domain.DomainName}'." );
            lock( _runSignal )
            {
                _stopped = true;
                System.Threading.Monitor.PulseAll( _runSignal );
            }
        }
    }
}
