using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Implementation of <see cref="IActionRegistrar{T}"/> that is handled by <see cref="AsyncExecutionContext{T}"/>.
    /// </summary>
    public class ActionRegistrar<T> : IActionRegistrar<T>
    {
        // Internal storage is based on Task to minimize the risks:
        // returned Tasks can safely be awaited multiple times.
        internal readonly List<Func<T, Task>> _actions;
        internal List<Func<T, Task>>? _onSuccess;
        internal List<Func<T, Exception, Task>>? _onError;
        internal List<Func<T, Task>>? _onFinally;
        // A registrar can be owned by zero or one ExecutionContext, and only once.
        object? _owner;

        static readonly string _successStep = "Currently handling success.";
        static readonly string _errorStep = "Currently handling error.";
        static readonly string _finallyStep = "Currently handling finalization.";
        string? _handlingStep;
        
        internal ActionRegistrar<T> AcquireOnce( object owner )
        {
            if( Interlocked.Exchange( ref _owner, owner ) == null ) return this;
            Throw.InvalidOperationException();
            // Unreached.
            return null!;
        }

        /// <summary>
        /// Initializes a new empty registrar.
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
            _actions.Add( action! );
        }

        /// <summary>
        /// Adds a new asynchronous action: this can be called during the execution of an action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        public void Add( Func<T, ValueTask> action )
        {
            GuardAdd( action == null );
            _actions.Add( c => action!( c ).AsTask() );
        }

        /// <summary>
        /// Adds a synchronous action: this can be called during the execution of an action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="action">The action to enqueue.</param>
        public void Add( Action<T> action )
        {
            GuardAdd( action == null );
            _actions.Add( c => { action!( c ); return Task.CompletedTask; } );
        }

        /// <summary>
        /// Adds a list of actions (that must be <see cref="Action{AsyncExecutionContext}"/>, <see cref="Func{AsyncExecutionContext, Task}"/>,
        /// or <see cref="Func{AsyncExecutionContext, ValueTask}"/> otherwise an <see cref="ArgumentException"/> is thrown).
        /// This can be called during the execution of an action by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> but not by error or success handlers. 
        /// </summary>
        /// <param name="actions">The actions to enqueue.</param>
        public void Add( IEnumerable<object> actions )
        {
            GuardAdd( actions == null );
            foreach( var a in actions! )
            {
                switch( a )
                {
                    case Func<T, Task> tA: _actions.Add( tA ); break;
                    case Func<T, ValueTask> vAsync: _actions.Add( c => vAsync( c ).AsTask() ); break;
                    case Action<T> sync: _actions.Add( c => { sync( c ); return Task.CompletedTask; } ); break;
                    default: Throw.ArgumentException( "Expected AsyncExecutionContext action function.", nameof( actions ) ); break;
                }
            }
        }

        /// <summary>
        /// Registers a new asynchronous success handler that will be called once all actions have been
        /// executed without errors.
        /// This can be called during the execution of any action or a success handler by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/>
        /// but not by an error handler. 
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        public void OnSuccess( Func<T, Task> handler )
        {
            GuardSuccess( handler == null );
            _onSuccess.Add( handler! );
        }

        /// <summary>
        /// Registers a new asynchronous success handler that will be called once all actions have been
        /// executed without errors.
        /// This can be called during the execution of any action or a success handler by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/>
        /// but not by an error handler. 
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        public void OnSuccess( Func<T, ValueTask> handler )
        {
            GuardSuccess( handler == null );
            _onSuccess.Add( c => handler!( c ).AsTask() );
        }

        /// <summary>
        /// Registers a new synchronous success handler that will be called once all actions have been
        /// executed without errors.
        /// This can be called during the execution of any action or a success handler by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/>
        /// but not by an error handler. 
        /// </summary>
        /// <param name="handler">The success handler to register.</param>
        public void OnSuccess( Action<T> handler )
        {
            GuardSuccess( handler == null );
            _onSuccess.Add( c => { handler!( c ); return Task.CompletedTask; } );
        }

        /// <summary>
        /// Registers a new asynchronous error handler: this can be called during the execution of any action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing an error handler (even if it is
        /// not recommended), but not while executing a success handler. 
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        public void OnError( Func<T, Exception, Task> errorHandler )
        {
            GuardError( errorHandler == null );
            _onError.Add( errorHandler! );
        }

        /// <summary>
        /// Registers a new asynchronous error handler: this can be called during the execution of any action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing an error handler (even if it is
        /// not recommended), but not while executing a success handler. 
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        public void OnError( Func<T, Exception, ValueTask> errorHandler )
        {
            GuardError( errorHandler == null );
            _onError.Add( ( c, ex ) => errorHandler!( c, ex ).AsTask() );
        }

        /// <summary>
        /// Registers a new synchronous error handler: this can be called during the execution of any action
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing an error handler (even if it is
        /// not recommended), but not while executing a success handler. 
        /// </summary>
        /// <param name="errorHandler">The error handler to register.</param>
        public void OnError( Action<T, Exception> errorHandler )
        {
            GuardError( errorHandler == null );
            _onError.Add( ( c, ex ) => { errorHandler!( c, ex ); return Task.CompletedTask; } );
        }

        /// <summary>
        /// Registers a new asynchronous finalization handler: this can be called during the execution of any action, success or error handler
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing another finalization handler (even if it is
        /// not recommended). 
        /// </summary>
        /// <param name="finalHandler">The final handler to register.</param>
        public void Finally( Func<T, Task> finalHandler )
        {
            GuardFinally( finalHandler == null );
            _onFinally.Add( finalHandler! );
        }

        /// <summary>
        /// Registers a new asynchronous finalization handler: this can be called during the execution of any action, success or error handler
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing another finalization handler (even if it is
        /// not recommended). 
        /// </summary>
        /// <param name="finalHandler">The final handler to register.</param>
        public void Finally( Func<T, ValueTask> finalHandler )
        {
            GuardFinally( finalHandler == null );
            _onFinally.Add( c => finalHandler!( c ).AsTask() );
        }

        /// <summary>
        /// Registers a new synchronous finalization handler: this can be called during the execution of any action, success or error handler
        /// by <see cref="AsyncExecutionContext{T}.ExecuteAsync"/> and even while executing another finalization handler (even if it is
        /// not recommended). 
        /// </summary>
        /// <param name="finalHandler">The final handler to register.</param>
        public void Finally( Action<T> finalHandler )
        {
            GuardFinally( finalHandler == null );
            _onFinally.Add( c => { finalHandler!( c ); return Task.CompletedTask; } );
        }

        internal void SetHandlingError() => _handlingStep = _errorStep;
        internal void SetHandlingSuccess() => _handlingStep = _successStep;
        internal void SetHandlingFinally() => _handlingStep = _finallyStep;
        internal void ClearHandling() => _handlingStep = null;

        void GuardAdd( bool nullArg )
        {
            if( nullArg ) Throw.ArgumentNullException( "action" );
            if( _handlingStep != null )
            {
                Throw.InvalidOperationException( _handlingStep );
            }
        }

        [MemberNotNull( nameof( _onSuccess ) )]
        void GuardSuccess( bool nullArg )
        {
            if( nullArg ) Throw.ArgumentNullException( "handler" );
            if( ReferenceEquals( _handlingStep, _errorStep ) || ReferenceEquals( _handlingStep, _finallyStep ) )
            {
                Throw.InvalidOperationException( _handlingStep );
            }
            if( _onSuccess == null ) _onSuccess = new List<Func<T, Task>>();
        }

        [MemberNotNull( nameof( _onError ) )]
        void GuardError( bool nullArg )
        {
            if( nullArg ) Throw.ArgumentNullException( "errorHandler" );
            if( ReferenceEquals( _handlingStep, _successStep ) || ReferenceEquals( _handlingStep, _finallyStep ) )
            {
                Throw.InvalidOperationException( _handlingStep );
            }
            if( _onError == null ) _onError = new List<Func<T, Exception, Task>>();
        }

        [MemberNotNull( nameof( _onFinally ) )]
        void GuardFinally( bool nullArg )
        {
            if( nullArg ) Throw.ArgumentNullException( "finalHandler" );
            if( _onFinally == null ) _onFinally = new List<Func<T, Task>>();
        }
    }
}
