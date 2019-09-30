using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Observable
{
    public static class ObservableDomainExtensions
    {
        /// <summary>
        /// Modifies this ObservableDomain, then executes any pending resulting actions.
        /// </summary>
        /// <typeparam name="TRoot">The type of the ObservableRootObject.</typeparam>
        /// <param name="this">The ObservableDomain.</param>
        /// <param name="monitor">The ActivityMonitor.</param>
        /// <param name="actions">The actions to execute inside the ObservableDomain's modification context.</param>
        /// <returns>The transaction result from <see cref="ObservableDomain.Modify"/>.</returns>
        public static async Task<TransactionResult> ModifyAsync<TRoot>( this ObservableDomain<TRoot> @this, IActivityMonitor monitor, Action actions )
            where TRoot : ObservableRootObject
        {
            var tr = @this.Modify( monitor, actions );

            await tr.ExecutePostActionsAsync( monitor );

            return tr;
        }
    }
}
