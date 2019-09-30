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

        /// <summary>
        /// Modifies this ObservableDomain, then executes any pending resulting actions.
        /// </summary>
        /// <typeparam name="TRoot1">The type of the ObservableRootObject #1.</typeparam>
        /// <typeparam name="TRoot2">The type of the ObservableRootObject #2.</typeparam>
        /// <param name="this">The ObservableDomain.</param>
        /// <param name="monitor">The ActivityMonitor.</param>
        /// <param name="actions">The actions to execute inside the ObservableDomain's modification context.</param>
        /// <returns>The transaction result from <see cref="ObservableDomain.Modify"/>.</returns>
        public static async Task<TransactionResult> ModifyAsync<TRoot1, TRoot2>( this ObservableDomain<TRoot1, TRoot2> @this, IActivityMonitor monitor, Action actions )
            where TRoot1 : ObservableRootObject
            where TRoot2 : ObservableRootObject
        {
            var tr = @this.Modify( monitor, actions );

            await tr.ExecutePostActionsAsync( monitor );

            return tr;
        }

        /// <summary>
        /// Modifies this ObservableDomain, then executes any pending resulting actions.
        /// </summary>
        /// <typeparam name="TRoot1">The type of the ObservableRootObject #1.</typeparam>
        /// <typeparam name="TRoot2">The type of the ObservableRootObject #2.</typeparam>
        /// <typeparam name="TRoot3">The type of the ObservableRootObject #3.</typeparam>
        /// <param name="this">The ObservableDomain.</param>
        /// <param name="monitor">The ActivityMonitor.</param>
        /// <param name="actions">The actions to execute inside the ObservableDomain's modification context.</param>
        /// <returns>The transaction result from <see cref="ObservableDomain.Modify"/>.</returns>
        public static async Task<TransactionResult> ModifyAsync<TRoot1, TRoot2, TRoot3>( this ObservableDomain<TRoot1, TRoot2, TRoot3> @this, IActivityMonitor monitor, Action actions )
            where TRoot1 : ObservableRootObject
            where TRoot2 : ObservableRootObject
            where TRoot3 : ObservableRootObject
        {
            var tr = @this.Modify( monitor, actions );

            await tr.ExecutePostActionsAsync( monitor );

            return tr;
        }
    }
}
