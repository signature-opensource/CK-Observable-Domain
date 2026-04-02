using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CK.Core;

// ReSharper disable once CheckNamespace
namespace CK.Observable;

/// <summary>
/// Base class for drivers that own a transient (in-memory, non-persisted) <see cref="ObservableDomain"/>.
/// <para>
/// On host start, the domain is created and configured inside a single initial transaction.
/// The configuration pipeline calls <see cref="PreConfigure"/>, then every registered
/// <see cref="IObservableDomainConfigurator{T}"/> for <typeparamref name="TSelf"/>, and finally <see cref="PostConfigure"/>.
/// </para>
/// </summary>
/// <typeparam name="TSelf">The concrete driver type for configurator resolution.</typeparam>
[CKTypeDefiner]
public abstract class TransientDomainDriver<TSelf> : IObservableDomainDriver, IRealObject
    where TSelf : TransientDomainDriver<TSelf>
{
    private readonly string _domainName;
    private readonly bool _autoStartTimer;

    private ObservableDomain _domain;
    private JsonEventCollector _eventCollector;

    /// <inheritdoc />
    public string DomainName => _domainName;

    /// <summary>
    /// Gets whether the domain's <see cref="ObservableDomain.TimeManager"/> is automatically started
    /// at the end of the initial configuration transaction.
    /// </summary>
    public bool AutoStartTimer => _autoStartTimer;

    /// <inheritdoc />
    public ObservableDomain Domain => _domain;

    /// <inheritdoc />
    public JsonEventCollector EventCollector => _eventCollector;

    /// <summary>
    /// Initializes a new <see cref="TransientDomainDriver{TSelf}"/>.
    /// </summary>
    /// <param name="domainName">
    /// The domain name. When null, defaults to the concrete type's <see cref="Type.FullName"/>.
    /// </param>
    /// <param name="autoStartTimer">True to start the domain's time manager after configuration.</param>
    protected TransientDomainDriver( string? domainName, bool autoStartTimer )
    {
        _domainName = domainName ?? GetType().FullName!;
        _autoStartTimer = autoStartTimer;
        _domain = null!;
        _eventCollector = null!;
    }

    private async Task OnHostStartAsync( IActivityMonitor monitor, IServiceProvider serviceProvider, IEnumerable<IObservableDomainConfigurator<TSelf>> configurators )
    {
        _domain = new ObservableDomain( monitor, _domainName, startTimer: false, serviceProvider );
        _eventCollector = new JsonEventCollector( _domain );

        await _domain.ModifyThrowAsync( monitor, () =>
        {
            PreConfigure( monitor, _domain );

            var success = true;
            foreach( var configurator in configurators )
            {
                success &= configurator.ConfigureDomain( monitor, _domain );
            }

            if( !success )
                Throw.InvalidOperationException( "One or more domain configurators failed." );

            PostConfigure( monitor, _domain );

            if( AutoStartTimer )
                _domain.TimeManager.Start();
        } );
    }

    /// <summary>
    /// Called inside the initial transaction, before any <see cref="IObservableDomainConfigurator{T}"/> runs.
    /// Override to create or initialize objects that configurators depend on.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domain">The domain being configured.</param>
    protected virtual void PreConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
    }

    /// <summary>
    /// Called inside the initial transaction, after all <see cref="IObservableDomainConfigurator{T}"/> have run.
    /// Override to finalize setup or validate the configured domain state.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domain">The domain being configured.</param>
    protected virtual void PostConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
    }

    /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor,Action,int,bool,bool,bool)" />
    protected Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                        Action<IActivityMonitor, ObservableDomain> actions,
                                                        int millisecondsTimeout = -1,
                                                        bool considerRolledbackAsFailure = true,
                                                        bool parallelDomainPostActions = true,
                                                        bool waitForDomainPostActionsCompletion = false )
    {
        Throw.CheckState( _domain is not null );
        return _domain.ModifyThrowAsync( monitor,
                                         () => actions.Invoke( monitor, _domain ),
                                         millisecondsTimeout,
                                         considerRolledbackAsFailure,
                                         parallelDomainPostActions,
                                         waitForDomainPostActionsCompletion );
    }

    /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync{TResult}(IActivityMonitor,Func{TResult},int,bool,bool)" />
    protected Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                       Func<IActivityMonitor, ObservableDomain, TResult> actions,
                                                       int millisecondsTimeout = -1,
                                                       bool parallelDomainPostActions = true,
                                                       bool waitForDomainPostActionsCompletion = false )
    {
        Throw.CheckState( _domain is not null );
        return _domain.ModifyThrowAsync( monitor,
                                         () => actions.Invoke( monitor, _domain ),
                                         millisecondsTimeout,
                                         parallelDomainPostActions,
                                         waitForDomainPostActionsCompletion );
    }

    /// <inheritdoc cref="ObservableDomain.TryModifyAsync(IActivityMonitor,Action,int,bool,bool,bool)" />
    protected Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                      Action<IActivityMonitor, ObservableDomain> actions,
                                                      int millisecondsTimeout = -1,
                                                      bool considerRolledbackAsFailure = true,
                                                      bool parallelDomainPostActions = true,
                                                      bool waitForDomainPostActionsCompletion = false )
    {
        Throw.CheckState( _domain is not null );
        return _domain.TryModifyAsync( monitor,
                                       () => actions.Invoke( monitor, _domain ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
    }

    /// <inheritdoc cref="ObservableDomain.ModifyAsync(IActivityMonitor,Action,bool,int,bool,bool,bool)" />
    protected Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                   Action<IActivityMonitor, ObservableDomain> actions,
                                                   bool throwException,
                                                   int millisecondsTimeout,
                                                   bool considerRolledbackAsFailure,
                                                   bool parallelDomainPostActions,
                                                   bool waitForDomainPostActionsCompletion )
    {
        Throw.CheckState( monitor is not null );
        return _domain.ModifyAsync( monitor,
                                    () => actions.Invoke( monitor, _domain ),
                                    throwException,
                                    millisecondsTimeout,
                                    considerRolledbackAsFailure,
                                    parallelDomainPostActions,
                                    waitForDomainPostActionsCompletion );
    }
}
