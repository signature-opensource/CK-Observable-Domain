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
            _queue = Channel.CreateUnbounded<TransactionResult>( new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true } );
        }

        internal void Enqueue( TransactionResult r )
        {
            if( _runSignal == null )
            {
                _runSignal = new object();
                _ = Run();
            }
            _queue.Writer.TryWrite( r );
        }

        internal bool Stop()
        {
            if( _runSignal != null )
            {
                Enqueue( TransactionResult.Empty );
                return true;
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

        internal async Task Run()
        {
            Debug.Assert( _runSignal != null );
            IActivityMonitor monitor = new ActivityMonitor( $"DomainPostAction executor for '{_domain.DomainName}'." ); ;
            var r = _queue.Reader;
            for( ; ; )
            {
                TransactionResult? t = await r.ReadAsync();
                if( t == TransactionResult.Empty )
                {
                    break;
                }
                var actions = await t.DomainActions;
                if( actions == null )
                {
                    monitor.Debug( $"Skipped domain '{_domain.DomainName}' transaction n°{t.TransactionNumber} DomainPostActions." );
                }
                else 
                {
                    var ctx = new PostActionContext( monitor, actions, t );
                    try
                    {
                        var result = await ctx.ExecuteAsync( throwException: false, name: $"domain '{_domain.DomainName}' transaction n°{t.TransactionNumber} DomainPostActions" );
                        t.SetDomainPostActionsResult( result );
                    }
                    finally
                    {
                        await ctx.DisposeAsync();
                    }
                }
            }
            lock( _runSignal )
            {
                _stopped = true;
                System.Threading.Monitor.Pulse( _runSignal );
            }
            monitor.MonitorEnd( $"Stopping DomainPostAction executor for '{_domain.DomainName}'." );
        }
    }
}
