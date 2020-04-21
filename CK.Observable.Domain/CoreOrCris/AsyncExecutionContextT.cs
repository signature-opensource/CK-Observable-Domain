using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Simple execution context for asynchronous (and synchronous) actions that provide them with a shared <see cref="Memory"/>,
    /// a <see cref="Monitor"/>, a trampoline (any action can enqueue one or more actions) and error handlers (that will
    /// be called on error).
    /// <para>
    /// This base class uses the (not funny) pattern of being parameterized by itself.
    /// It must be specialized with its own specialization to support additional features.
    /// However, once done, the easiest (and most modular) way to extend this is simply to use extension methods backed by the <see cref="Memory"/>.
    /// </para>
    /// </summary>
    public abstract class AsyncExecutionContext<TThis> : IAsyncDisposable where TThis : AsyncExecutionContext<TThis>
    {
        readonly ActionRegisterer<TThis> _reg;
        readonly IActivityMonitor _monitor;
        IDictionary<object, object> _memory;
        readonly bool _callMemoryDisposable;
        bool _executing;

        /// <summary>
        /// Initializes an asynchronous execution context.
        /// </summary>
        /// <param name="monitor">The monitor that must be used.</param>
        /// <param name="registerer">Optional existing registerer.</param>
        public AsyncExecutionContext( IActivityMonitor monitor, ActionRegisterer<TThis>? registerer = null )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            _reg = (registerer ?? new ActionRegisterer<TThis>()).AcquireOnce( this );
            _callMemoryDisposable = true;
            _monitor = monitor;
        }

        /// <summary>
        /// Initializes an execution context bound to an external memory.
        /// </summary>
        /// <param name="monitor">The monitor that must be used.</param>
        /// <param name="externalMemory">External memory.</param>
        /// <param name="callMemoryDisposable">
        /// True to call <see cref="IAsyncDisposable.DisposeAsync"/> or <see cref="IDisposable.Dispose"/> on
        /// all disposable <see cref="Memory"/>'s values.
        /// </param>
        public AsyncExecutionContext( IActivityMonitor monitor, ActionRegisterer<TThis>? registerer, IDictionary<object, object> externalMemory, bool callMemoryDisposable )
            : this( monitor, registerer )
        {
            if( externalMemory == null ) throw new ArgumentNullException( nameof( externalMemory ) );
            _memory = externalMemory;
            _callMemoryDisposable = callMemoryDisposable;
        }

        /// <summary>
        /// Gets a memory that can be used to share state between actions.
        /// The registered values that support <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> will be
        /// automatically disposed when this context is <see cref="DisposeAsync"/> (except when using the special
        /// contructor <see cref="AsyncExecutionContext(IActivityMonitor,IDictionary{object, object},bool)"/>).
        /// </summary>
        public IDictionary<object, object> Memory => _memory ?? (_memory = new Dictionary<object, object>());

        /// <summary>
        /// Gets the monitor to use.
        /// </summary>
        public IActivityMonitor Monitor { get; }

        /// <summary>
        /// Gets the registerer. Actions and/or error handlers can be registered
        /// even when <see cref="ExecuteAsync(bool, bool)"/> has been called.
        /// </summary>
        public ActionRegisterer<TThis> Registerer { get; }

        /// <summary>
        /// Executes the currently enlisted actions, optionaly in reverse order.
        /// On the first exception thrown by any action, all the error handlers are called (their own axceptions
        /// if any are ignored).
        /// </summary>
        /// <param name="throwException">False to return any exception instead of logging and rethrowing it.</param>
        /// <returns>The first exception that occurred or null on success.</returns>
        public async Task<Exception?> ExecuteAsync( bool throwException = true, bool reverseInitialActions = false )
        {
            if( _executing ) throw new InvalidOperationException( "ExecuteAsync reentrancy detected." );
            var actions = _reg._actions;
            if( reverseInitialActions ) actions.Reverse();
            using( _monitor.OpenInfo( $"Executing {actions.Count} initial actions{(reverseInitialActions ? " in reverse order" : "")}." ) )
            {
                _executing = true;
                int idxCulprit = 0;
                try
                {
                    int roundNumber = 0;
                    int roundCount;
                    while( (roundCount = actions.Count) > 0 )
                    {
                        using( Monitor.OpenTrace( $"Executing round n°{++roundNumber} with {roundCount} actions." ) )
                        {
                            while( idxCulprit < roundCount )
                            {
                                await actions[idxCulprit].Invoke( (TThis)this );
                                ++idxCulprit;
                            }
                        }
                        idxCulprit = 0;
                        actions.RemoveRange( 0, roundCount );
                    }
                    var onSuccess = _reg._onSuccess;
                    if( onSuccess == null ) _monitor.Trace( "There is no registered success handler." );
                    else
                    {
                        using( _monitor.OpenTrace( $"Calling {onSuccess.Count} success handlers." ) )
                        {
                            await RaiseSuccess( onSuccess );
                        }
                    }
                    return null;
                }
                catch( Exception ex )
                {
                    actions.RemoveRange( 0, idxCulprit );
                    using( _monitor.OpenError( ex ) )
                    {
                        if( _reg._onError == null ) _monitor.Trace( "There is no registered error handler." );
                        else
                        {
                            using( _monitor.OpenTrace( $"Calling {_reg._onError.Count} error handlers." ) )
                            {
                                await RaiseError( _reg._onError, ex );
                            }
                        }
                        if( throwException )
                        {
                            _monitor.Trace( "Rethrowing exception." );
                            throw;
                        }
                    }
                    return ex;
                }
                finally
                {
                    _executing = false;
                }
            }
        }

        async Task RaiseSuccess( List<Func<TThis, Task>> success )
        {
            _reg.SetHandlingSuccess();
            int roundNumber = 0;
            int roundCount;
            while( (roundCount = success.Count) > 0 )
            {
                using( Monitor.OpenTrace( $"Executing Success handlers round n°{++roundNumber} with {roundCount} handlers." ) )
                {
                    for( int i = 0; i < roundCount; ++i )
                    {
                        try
                        {
                            await success[i].Invoke( (TThis)this );
                        }
                        catch( Exception ex )
                        {
                            _monitor.Error( "While executing success handler. This is ignored.", ex );
                        }
                    }
                    success.RemoveRange( 0, roundCount );
                }
            }
            _reg.ClearHandling();
        }

        async ValueTask RaiseError( List<Func<TThis, Exception, Task>> errors, Exception ex )
        {
            _reg.SetHandlingError();
            int roundNumber = 0;
            int roundCount;
            while( (roundCount = errors.Count) > 0 )
            {
                using( Monitor.OpenTrace( $"Executing Error handling round n°{++roundNumber} with {roundCount} handlers." ) )
                {
                    for( int i = 0; i < roundCount; ++i )
                    {
                        try
                        {
                            await errors[i].Invoke( (TThis)this, ex );
                        }
                        catch( Exception exError )
                        {
                            _monitor.Error( "While handling error. This is ignored.", exError );
                        }
                    }
                    errors.RemoveRange( 0, roundCount );
                }
            }
            _reg.ClearHandling();
        }

        /// <summary>
        /// Disposes this execution context.
        /// </summary>
        /// <returns>The awaitable.</returns>
        public async ValueTask DisposeAsync()
        {
            if( _executing ) throw new InvalidOperationException( "Currently executing." );
            if( _callMemoryDisposable && _memory != null )
            {
                foreach( var kv in _memory )
                {
                    try
                    {
                        if( kv.Value is IAsyncDisposable ad ) await ad.DisposeAsync();
                        else if( kv.Value is IDisposable d ) d.Dispose();
                    }
                    catch( Exception ex )
                    {

                        _monitor.Error( $"While disposing AsyncExecutionContext's memory [{Safe( kv.Key )}] = {Safe( kv.Value )}.", ex );
                    }
                }
            }

            static string Safe( object? o )
            {
                try
                {
                    return o?.ToString() ?? "<null>";
                }
                catch( Exception ex )
                {
                    return $"<Exception '{ex.Message}' while calling ToString() on a '{o.GetType().Name}'>";
                }
            }
        }
    }


}
