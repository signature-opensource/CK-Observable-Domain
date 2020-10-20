using CK.Core;
using System;

namespace CK.Observable
{
    /// <summary>
    /// Extension point to defer the execution of <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> into
    /// another context.
    /// </summary>
    public interface IPostActionContextMarshaller 
    {
        /// <summary>
        /// Must captures the post actions so that its <see cref="AsyncExecutionContext{TThis}.ExecuteAsync(bool, bool)"/> can be
        /// executed on another context, typically a background service.
        /// <para>
        /// When returning false, the post actions will be executed as usual by the <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/>.
        /// </para>
        ///<para>
        /// This method must not throw: when returning true, it just has to enqueue the <paramref name="postActions"/>
        /// and optionally the <paramref name="modifyThrowCalled"/>.
        /// Please note that an <see cref="AsyncExecutionContext{TThis}"/> is disposable: once executed, it should be disposed.
        ///</para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="postActions">The post actions that must be executed.</param>
        /// <param name="modifyThrowCalled">
        /// Whether <see cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action, int)"/> or <see cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action, int)"/>
        /// have been called: exceptions are exepected to be thrown. This is for information since this marshaller cannot "reinject" the future exceptions
        /// into the original Modify call.
        /// </param>
        /// <returns>
        /// True to indicate that the actions will be executed elsewhere, false to let the TransactionResult execute them as usual.
        /// </returns>
        bool MarshallExecution( IActivityMonitor monitor, PostActionContext postActions, bool modifyThrowCalled );
    }
}
