using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Implementation of <see cref="IActionRegistrar{T}"/> that is handled by <see cref="AsyncExecutionContext{T}"/>.
    /// </summary>
    public class ActionRegistrar<T> : IActionRegistrar<T>
    {
        // Internal storage is based on Task to minimise the risks:
        // returned Tasks can safely be awaited multiple times.
        internal readonly List<Func<T, Task>> _actions;
        internal List<Func<T, Task>> _onSuccess;
        internal List<Func<T, Exception, Task>> _onError;
        // A registerer can can be owned by zero or one ExecutionContext, and only once.
        object _owner;

        static readonly string _successStep = "Currently handling success.";
        static readonly string _errorStep = "Currently handling error.";
        string _handlingStep;
        
        internal ActionRegistrar<T> AcquireOnce( object owner )
        {
            if( Interlocked.Exchange( ref _owner, owner ) == null ) return this;
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Initializes a new empty registerer.
        /// </summary>
        public ActionRegistrar()
        {
            _actions = new List<Func<T, Task>>();
        }

        /// <summary>
        /// Gets the number of actions that should be executed.
        /// </summary>
        public int ActionCount => _actions.Count;

        /// <summary>
        /// Adds a new asynchronous action: this can be called during the execution of an action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        public void Add( Func<T, Task> action )
        {
            GuardAdd( action == null );
            _actions.Add( action );
        }

        /// <summary>
        /// Adds a new asynchronous action: this can be called during the execution of an action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        public void Add( Func<T, ValueTask> action )
        {
            GuardAdd( action == null );
            _actions.Add( c => action( c ).AsTask() );
        }

        /// <summary>
        /// Adds a synchronous action: this can be called during the execution of an action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        public void Add( Action<T> action )
        {
            GuardAdd( action == null );
            _actions.Add( c => { action( c ); return Task.CompletedTask; } );
        }

        /// <summary>
        /// Adds a list of actions (that must be <see cref="Action{AsyncExecutionContext}"/>, <see cref="Func{AsyncExecutionContext, Task}"/>,
        /// or <see cref="Func{AsyncExecutionContext, ValueTask}"/> otherwise an <see cref="ArgumentException"/> is thrown).
        /// This can be called during the execution of an action by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        public void Add( IEnumerable<object> actions )
        {
            GuardAdd( actions == null );
            foreach( var a in actions )
            {
                switch( a )
                {
                    case Func<T, Task> tA: _actions.Add( tA ); break;
                    case Func<T, ValueTask> vAsync: _actions.Add( c => vAsync( c ).AsTask() ); break;
                    case Action<T> sync: _actions.Add( c => { sync( c ); return Task.CompletedTask; } ); break;
                    default: throw new ArgumentException( "Expected AsyncExecutionContext action function.", nameof( actions ) );
                }
            }
        }

        /// <summary>
        /// Registers a new asynchronous success handler that will be called once all actions have been
        /// executed without errors.
        /// This can be called during the execution of any action or a success handler by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/>
        /// but not by an error hanlder. 
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        public void OnSuccess( Func<T, Task> handler )
        {
            GuardSucces( handler == null );
            _onSuccess.Add( handler );
        }

        /// <summary>
        /// Registers a new asynchronous success handler that will be called once all actions have been
        /// executed without errors.
        /// This can be called during the execution of any action or a success handler by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/>
        /// but not by an error hanlder. 
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        public void OnSuccess( Func<T, ValueTask> handler )
        {
            GuardSucces( handler == null );
            _onSuccess.Add( c => handler( c ).AsTask() );
        }

        /// <summary>
        /// Registers a new synchronous success handler that will be called once all actions have been
        /// executed without errors.
        /// This can be called during the execution of any action or a success handler by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/>
        /// but not by an error hanlder. 
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        public void OnSuccess( Action<T> handler )
        {
            GuardSucces( handler == null );
            _onSuccess.Add( c => { handler( c ); return Task.CompletedTask; } );
        }

        /// <summary>
        /// Registers a new asynchronous error handler: this can be called during the execution of any action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing an error handler (even if it is
        /// not recommended), but not while executing a success hanlder. 
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        public void OnError( Func<T, Exception, Task> errorHandler )
        {
            GuardError( errorHandler == null );
            _onError.Add( errorHandler );
        }

        /// <summary>
        /// Registers a new asynchronous error handler: this can be called during the execution of any action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing an error handler (even if it is
        /// not recommended), but not while executing a success hanlder. 
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        public void OnError( Func<T, Exception, ValueTask> errorHandler )
        {
            GuardError( errorHandler == null );
            _onError.Add( ( c, ex ) => errorHandler( c, ex ).AsTask() );
        }

        /// Registers a new synchronous error handler: this can be called during the execution of any action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing an error handler (even if it is
        /// not recommended), but not while executing a success hanlder. 
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        public void OnError( Action<T, Exception> errorHandler )
        {
            GuardError( errorHandler == null );
            _onError.Add( ( c, ex ) => { errorHandler( c, ex ); return Task.CompletedTask; } );
        }

        internal void SetHandlingError() => _handlingStep = _errorStep;
        internal void SetHandlingSuccess() => _handlingStep = _successStep;
        internal void ClearHandling() => _handlingStep = null;

        void GuardAdd( bool nullArg )
        {
            if( nullArg ) throw new ArgumentNullException( "action" );
            if( _handlingStep != null ) throw new InvalidOperationException( _handlingStep );
        }

        void GuardSucces( bool nullArg )
        {
            if( nullArg ) throw new ArgumentNullException( "handler" );
            if( ReferenceEquals( _handlingStep, _errorStep ) ) throw new InvalidOperationException( _handlingStep );
            if( _onSuccess == null ) _onSuccess = new List<Func<T, Task>>();
        }

        void GuardError( bool nullArg )
        {
            if( nullArg ) throw new ArgumentNullException( "errorHandler" );
            if( ReferenceEquals( _handlingStep, _successStep ) ) throw new InvalidOperationException( _handlingStep );
            if( _onError == null ) _onError = new List<Func<T, Exception, Task>>();
        }

    }
}
