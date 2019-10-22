using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CK.Core;
using System.Threading;

namespace CK.Observable
{
    /// <summary>
    /// Default implementation that honors the <see cref="TransactionResult.NextDueTimeUtc"/> by
    /// using a <see cref="System.Threading.Timer"/>.
    /// 
    /// </summary>
    public class StandardPostTransactionHook : IPostTransactionHook
    {
        readonly Timer _timer;
        readonly ObservableDomain _domain;
        long _next;

        public StandardPostTransactionHook( ObservableDomain domain )
        {
            _domain = domain ?? throw new ArgumentNullException( nameof( domain ) );
            _timer = new Timer( new TimerCallback( OnTime ), this, Timeout.Infinite, Timeout.Infinite );
        }

        static void OnTime( object state )
        {
            var c = (StandardPostTransactionHook)state;
            var m = new ActivityMonitor( "Raising timer from StandardPostTransactionHook." );
            try
            {
                c._domain.FromTimerModifyAsync();
            }
            catch( Exception ex )
            {
                m.Error( ex );
            }
            m.MonitorEnd();
        }

        public void OnTransactionDone( IActivityMonitor monitor, ObservableDomain origin, TransactionResult result )
        {
            if( _domain != origin ) throw new ArgumentException( $"Origin must be domain '{_domain.DomainName}', not '{origin?.DomainName ?? "<null>"}'.", nameof( origin ) );
            
            Interlocked.CompareExchange()
            _timer.Change( )
            t.
        }
    }
}
