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
        readonly TimerCallback _timerFunc;

        public StandardPostTransactionHook()
        {
            _timerFunc = new TimerCallback( OnTime );
        }

        class Context
        {
            public readonly ObservableDomain Domain;
            public readonly Timer Timer;
        }

        void OnTime( object state )
        {
            var c = (Context)state;
            var r = c.Domain.ModifyAsync( )
        }

        public void OnTransactionDone( IActivityMonitor monitor, ObservableDomain domain, TransactionResult result )
        {
            
        }
    }
}
