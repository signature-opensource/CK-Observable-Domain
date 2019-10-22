using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CK.Core;

namespace CK.Observable
{

    /// <summary>
    /// Optional interface that can be set on <see cref="ObservableDomain.PostTransactionHook"/> and is called whatever the
    /// result of the transactio is.
    /// This is a very basic model that enables actual implementation to be bound to a unique <see cref="ObservableDomain"/> or
    /// shared by multiple ones (bound implementations should check that the origin domain is always the same).
    /// </summary>
    public interface IPostTransactionHook
    {
        /// <summary>
        /// Called once each transaction is terminated after having released the write lock: domain objects
        /// should not be sollicitated anymore (unless <see cref="ObservableDomain.AcquireReadLock(int)"/>
        /// or <see cref="ObservableDomain.BeginTransaction(IActivityMonitor, int)"/> are called).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="origin">The origin domain. No lock protect it.</param>
        /// <param name="result">The transaction result.</param>
        void OnTransactionDone( IActivityMonitor monitor, ObservableDomain origin, TransactionResult result );
    }
}
