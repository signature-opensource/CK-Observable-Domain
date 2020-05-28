using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Registerer for asynchronous and synchronous actions and error handlers
    /// that operates on a <typeparamref name="T"/> parameter.
    /// </summary>
    public interface IActionRegistrar<T>
    {
        /// <summary>
        /// Adds a new asynchronous action.
        /// The action itself can add other actions and/or register error or success handlers.
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        void Add( Func<T, Task> action );

        /// <summary>
        /// Adds a new asynchronous action. 
        /// The action itself can add other actions and/or register error or success handlers.
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        void Add( Func<T, ValueTask> action );

        /// <summary>
        /// Adds a synchronous action. 
        /// The action itself can add other actions and/or register error or success handlers.
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        void Add( Action<T> action );

        /// <summary>
        /// Adds a list of actions (that must be <see cref="Action{T}"/>, <see cref="Func{T, Task}"/>,
        /// or <see cref="Func{T, ValueTask}"/> otherwise an <see cref="ArgumentException"/> is thrown).
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        void Add( IEnumerable<object> actions );

        /// <summary>
        /// Registers a new asynchronous success handler.
        /// This will be called after the last action, any exception thrown by the handler
        /// will be logged and ignored. A success handler is not allowed to register any
        /// new action or error handler but it can register another success handler if needed.
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        void OnSuccess( Func<T, Task> handler );

        /// <summary>
        /// Registers a new asynchronous success handler. 
        /// This will be called after the last action, any exception thrown by the handler
        /// will be logged and ignored. A success handler is not allowed to register any
        /// new action or error handler but it can register another success handler if needed.
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        void OnSuccess( Func<T, ValueTask> handler );

        /// <summary>
        /// Registers a new synchronous success handler. 
        /// This will be called after the last action, any exception thrown by the handler
        /// will be logged and ignored. A success handler is not allowed to register any
        /// new action or error handler but it can register another success handler if needed.
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        void OnSuccess( Action<T> handler );

        /// <summary>
        /// Registers a new asynchronous error handler.
        /// This will be called if an action throws an exception. Any exception thrown by this handler
        /// will be logged and ignored. An error handler is not allowed to register any
        /// new action or success handler but it can register another error handler if needed.
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        void OnError( Func<T, Exception, Task> errorHandler );

        /// <summary>
        /// Registers a new asynchronous error handler. 
        /// This will be called if an action throws an exception. Any exception thrown by this handler
        /// will be logged and ignored. An error handler is not allowed to register any
        /// new action or success handler but it can register another error handler if needed.
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        void OnError( Func<T, Exception, ValueTask> errorHandler );

        /// <summary>
        /// Registers a new synchronous error handler. 
        /// This will be called if an action throws an exception. Any exception thrown by this handler
        /// will be logged and ignored. An error handler is not allowed to register any
        /// new action or success handler but it can register another error handler if needed.
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        void OnError( Action<T, Exception> errorHandler );

        /// <summary>
        /// Registers a new asynchronous final handler.
        /// This will be called after success or error handlers. Any exception thrown by this handler
        /// will be logged and ignored. A final handler is not allowed to register any
        /// new action, success or error handler but it can register another final handler if needed.
        /// </summary>
        /// <param name="finalHandler">The final handler to register.</param>
        void Finally( Func<T, Task> finalHandler );

        /// <summary>
        /// Registers a new asynchronous final handler. 
        /// This will be called after success or error handlers. Any exception thrown by this handler
        /// will be logged and ignored. A final handler is not allowed to register any
        /// new action, success or error handler but it can register another final handler if needed.
        /// </summary>
        /// <param name="finalHandler">The final handler to register.</param>
        void Finally( Func<T, ValueTask> finalHandler );

        /// <summary>
        /// Registers a new synchronous final handler. 
        /// This will be called after success or error handlers. Any exception thrown by this handler
        /// will be logged and ignored. A final handler is not allowed to register any
        /// new action, success or error handler but it can register another final handler if needed.
        /// </summary>
        /// <param name="finalHandler">The final handler to register.</param>
        void Finally( Action<T> finalHandler );
    }
}
