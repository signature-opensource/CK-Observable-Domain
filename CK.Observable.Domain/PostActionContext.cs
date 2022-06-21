using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// The context in which <see cref="TransactionDoneEventArgs.PostActions"/> and <see cref="TransactionDoneEventArgs.DomainPostActions"/>
    /// are executed.
    /// </summary>
    public class PostActionContext : AsyncExecutionContext<PostActionContext>
    {
        internal PostActionContext( IActivityMonitor monitor, ActionRegistrar<PostActionContext> actions, TransactionResult result )
            : base( monitor, actions )
        {
            TransactionResult = result;
        }

        /// <summary>
        /// Gets the <see cref="TransactionResult"/>.
        /// </summary>
        public TransactionResult TransactionResult { get; }
    }
}
