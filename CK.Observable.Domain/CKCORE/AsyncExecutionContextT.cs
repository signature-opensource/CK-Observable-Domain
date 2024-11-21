using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Core;

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
public abstract class AsyncExecutionContext<TSelf> : IAsyncDisposable where TSelf : AsyncExecutionContext<TSelf>
{
    readonly ActionRegistrar<TSelf> _reg;
    readonly IActivityMonitor _monitor;
    IDictionary<object, object>? _memory;
    readonly bool _callMemoryDisposable;
    bool _executing;

    /// <summary>
    /// Initializes an asynchronous execution context.
    /// By default all disposable <see cref="Memory"/>'s values are disposed with <see cref="IAsyncDisposable.DisposeAsync"/> or <see cref="IDisposable.Dispose"/>.
    /// </summary>
    /// <param name="monitor">The monitor that must be used.</param>
    /// <param name="registrar">Optional existing registrar.</param>
    public AsyncExecutionContext( IActivityMonitor monitor, ActionRegistrar<TSelf>? registrar = null )
    {
        if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
        _reg = (registrar ?? new ActionRegistrar<TSelf>()).AcquireOnce( this );
        _callMemoryDisposable = true;
        _monitor = monitor;
    }

    /// <summary>
    /// Initializes an execution context bound to an external memory.
    /// </summary>
    /// <param name="monitor">The monitor that must be used.</param>
    /// <param name="registrar">Optional existing registrar.</param>
    /// <param name="externalMemory">External memory.</param>
    /// <param name="callMemoryDisposable">
    /// True to call <see cref="IAsyncDisposable.DisposeAsync"/> or <see cref="IDisposable.Dispose"/> on
    /// all disposable <see cref="Memory"/>'s values.
    /// </param>
    public AsyncExecutionContext( IActivityMonitor monitor, ActionRegistrar<TSelf>? registrar, IDictionary<object, object> externalMemory, bool callMemoryDisposable )
        : this( monitor, registrar )
    {
        if( externalMemory == null ) throw new ArgumentNullException( nameof( externalMemory ) );
        _memory = externalMemory;
        _callMemoryDisposable = callMemoryDisposable;
    }

    /// <summary>
    /// Gets a memory that can be used to share state between actions.
    /// The registered values that support <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> will be
    /// automatically disposed when this context is <see cref="DisposeAsync"/> (except when using the special
    /// constructor <see cref="AsyncExecutionContext(IActivityMonitor,IDictionary{object, object},bool)"/>).
    /// </summary>
    public IDictionary<object, object> Memory => _memory ?? (_memory = new Dictionary<object, object>());

    /// <summary>
    /// Gets the monitor to use.
    /// </summary>
    public IActivityMonitor Monitor => _monitor;

    /// <summary>
    /// Gets the registrar. Actions, error, success and/or finally handlers can be registered
    /// even when <see cref="ExecuteAsync(bool, bool)"/> has been called.
    /// </summary>
    public ActionRegistrar<TSelf> Registrar => _reg;

    /// <summary>
    /// Executes the currently enlisted actions, optionally in reverse order.
    /// On the first exception thrown by any action, all the error handlers are called (their own exceptions,
    /// if any, are logged but ignored).
    /// </summary>
    /// <param name="throwException">False to return any exception instead of logging and re throwing it.</param>
    /// <param name="reverseInitialActions">
    /// True to revert the initial action list: the last registered will be the first to be called.
    /// </param>
    /// <param name="name">Optional name that tags the execution (for logs).</param>
    /// <returns>The first exception that occurred or null on success.</returns>
    public async Task<Exception?> ExecuteAsync( bool throwException = true, bool reverseInitialActions = false, string name = "(unnamed)" )
    {
        Throw.CheckState( !_executing );
        var actions = _reg._actions;
        if( reverseInitialActions ) actions.Reverse();
        using( _monitor.OpenInfo( $"[{name}]: {actions.Count} initial actions{(reverseInitialActions ? " in reverse order" : "")}." ) )
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
                            await actions[idxCulprit].Invoke( (TSelf)this ).ConfigureAwait( false );
                            ++idxCulprit;
                        }
                    }
                    idxCulprit = 0;
                    actions.RemoveRange( 0, roundCount );
                }
                var onSuccess = _reg._onSuccess;
                if( onSuccess == null ) _monitor.Debug( "There is no registered success handler." );
                else
                {
                    using( _monitor.OpenTrace( $"Calling {onSuccess.Count} success handlers." ) )
                    {
                        await RaiseSuccessAsync( onSuccess, name ).ConfigureAwait( false );
                    }
                }
                return null;
            }
            catch( Exception ex )
            {
                actions.RemoveRange( 0, idxCulprit );
                using( _monitor.OpenError( $"[{name}]: error, leaving {actions.Count} not executed actions.", ex ) )
                {
                    if( _reg._onError == null ) _monitor.Trace( "There is no registered error handler." );
                    else
                    {
                        using( _monitor.OpenInfo( $"Calling {_reg._onError.Count} error handlers." ) )
                        {
                            await RaiseErrorAsync( _reg._onError, ex, name ).ConfigureAwait( false );
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
                if( _reg._onFinally == null ) _monitor.Debug( "There is no registered final handler." );
                else
                {
                    using( _monitor.OpenInfo( $"Calling {_reg._onFinally.Count} final handlers." ) )
                    {
                        await RaiseFinallyAsync( _reg._onFinally, name ).ConfigureAwait( false );
                    }
                }
                _executing = false;
            }
        }
    }

    /// <summary>
    /// Executes the registered success handlers. This never throws.
    /// </summary>
    /// <param name="success">The success handlers.</param>
    /// <param name="name">Execution name (for logs).</param>
    /// <returns>The awaitable.</returns>
    async Task RaiseSuccessAsync( List<Func<TSelf, Task>> success, string name )
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
                        await success[i].Invoke( (TSelf)this ).ConfigureAwait( false );
                    }
                    catch( Exception ex )
                    {
                        _monitor.Error( $"[{name}]: While executing success handler. This is ignored.", ex );
                    }
                }
                success.RemoveRange( 0, roundCount );
            }
        }
        _reg.ClearHandling();
    }

    /// <summary>
    /// Executes the error handlers. Never throws.
    /// </summary>
    /// <param name="errors">The error handlers.</param>
    /// <param name="ex">The exception that has been raised by the action.</param>
    /// <param name="name">Execution name (for logs).</param>
    /// <returns>The awaitable.</returns>
    async ValueTask RaiseErrorAsync( List<Func<TSelf, Exception, Task>> errors, Exception ex, string name )
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
                        await errors[i].Invoke( (TSelf)this, ex ).ConfigureAwait( false );
                    }
                    catch( Exception exError )
                    {
                        _monitor.Error( $"[{name}]: While handling error. This is ignored.", exError );
                    }
                }
                errors.RemoveRange( 0, roundCount );
            }
        }
        _reg.ClearHandling();
    }

    /// <summary>
    /// Executes the registered finally actions. This never throws.
    /// </summary>
    /// <param name="final">The final actions to execute.</param>
    /// <param name="name">Execution name (for logs).</param>
    /// <returns>The awaitable.</returns>
    async Task RaiseFinallyAsync( List<Func<TSelf, Task>> final, string name )
    {
        _reg.SetHandlingFinally();
        int roundNumber = 0;
        int roundCount;
        while( (roundCount = final.Count) > 0 )
        {
            using( Monitor.OpenTrace( $"Executing Final handlers round n°{++roundNumber} with {roundCount} handlers." ) )
            {
                for( int i = 0; i < roundCount; ++i )
                {
                    try
                    {
                        await final[i].Invoke( (TSelf)this ).ConfigureAwait( false );
                    }
                    catch( Exception ex )
                    {
                        _monitor.Error( $"[{name}]: While executing final handler. This is ignored.", ex );
                    }
                }
                final.RemoveRange( 0, roundCount );
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
                    if( kv.Value is IAsyncDisposable ad ) await ad.DisposeAsync().ConfigureAwait( false );
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
                return $"<Exception '{ex.Message}' while calling ToString() on a '{o?.GetType().Name}'>";
            }
        }
    }
}
