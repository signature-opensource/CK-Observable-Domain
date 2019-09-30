using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Observable
{
    public static class ObservableDomainExtensions
    {
        /// <summary>
        /// Modifies this ObservableDomain, then executes any pending post-actions.
        /// </summary>
        /// <typeparam name="TDomain">The type of the ObservableDomain.</typeparam>
        /// <param name="this">The ObservableDomain.</param>
        /// <param name="monitor">The ActivityMonitor.</param>
        /// <param name="actions">The actions to execute inside the ObservableDomain's modification context.</param>
        /// <returns>The transaction result from <see cref="ObservableDomain.Modify"/>.</returns>
        public static async Task<TransactionResult> ModifyAsync<TDomain>( this TDomain @this, IActivityMonitor monitor, Action actions )
            where TDomain : ObservableDomain
        {
            var tr = @this.Modify( monitor, actions );

            await tr.ExecutePostActionsAsync( monitor );

            return tr;
        }
    }
}
