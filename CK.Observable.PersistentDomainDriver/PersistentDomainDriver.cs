using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.AppIdentity;
using CK.Core;
using Microsoft.Extensions.Configuration;

namespace CK.Observable;

/// <summary>
/// Base class for drivers that own a persistent (file-backed) <see cref="ObservableDomain"/>:
/// the non-transient counterpart of <see cref="TransientDomainDriver{TSelf}"/>.
/// <para>
/// The driver keeps the domain in a folder of generation files. Each save writes a new file with the next
/// generation number. The driver keeps the newest <see cref="PersistentDomainOptions.GenerationCount"/> files.
/// The folder is <see cref="PersistentDomainOptions.StorePath"/>. If this option is null, the folder is
/// "ObservableDomains/&lt;DomainName&gt;" in the local file store of the application identity.
/// One process at a time owns the folder: the driver holds a lock file while the host runs.
/// </para>
/// <para>
/// At start, the recovery chain loads the newest valid generation. If the newest file is damaged, the driver
/// can try older files (<see cref="PersistentDomainOptions.FallbackGenerationCount"/>). If no allowed file loads,
/// the start fails, or the driver creates a new domain when <see cref="PersistentDomainOptions.CreateOnLoadError"/> is true.
/// <see cref="LoadOutcome"/> tells what happened.
/// </para>
/// <para>
/// The load stops the timer of the domain. Then the driver calls each <see cref="IPersistentDomainLoadHandler{T}"/>
/// of <typeparamref name="TSelf"/>. A handler can stop the start. Then <see cref="PreConfigure"/>,
/// <see cref="InitializeNewDomain"/>, every registered <see cref="IObservableDomainConfigurator{T}"/> for
/// <typeparamref name="TSelf"/> and <see cref="PostConfigure"/> run inside a single initial transaction. If
/// <see cref="AutoStartTimer"/> is true, the last step of this transaction starts the timer. The start does not wait
/// for the domain post actions of this transaction.
/// </para>
/// <para>
/// Only after the initial transaction succeeds, the driver attaches its saver and writes the first save of the start.
/// This save marks the files that the driver did not load just before the rename of its file.
/// So a start that fails before its first save changes no existing file: only the lock file and the deletion of
/// orphan temporary files touch the folder. The marks keep the files that the driver did not load out of the count retention.
/// </para>
/// <para>
/// All of them except <see cref="InitializeNewDomain"/> run on EVERY start, against a new or a
/// loaded domain: they must be idempotent. <see cref="DomainView.CreateSingleton{T}"/> finds or creates
/// a singleton, so use it to install singletons. Each call increments the instantiation count of the
/// singleton (one for each host start). This is important only for code that expects one Create for one Destroy.
/// Seeding that must not run against a loaded domain goes in <see cref="InitializeNewDomain"/>,
/// the counterpart of the League's IObservableDomainInitializer.
/// </para>
/// <para>
/// The "CK-ObservableDomain" configuration section holds the options of each domain
/// (see <see cref="PersistentDomainDriverOptions"/>). <see cref="PersistentDomainOptions.SnapshotSaveDelay"/>
/// sets when a commit writes a generation. The driver saves with no transaction of its own: a save that no commit
/// carries copies the committed state under the domain read lock. A graceful host stop writes a final generation
/// if the last committed state is not saved yet, releases the lock, then disposes the domain.
/// </para>
/// <para>
/// This driver is in its own package, not in CK.Observable.Domain: so only its consumers get a dependency on CK.AppIdentity.
/// </para>
/// </summary>
/// <typeparam name="TSelf">The concrete driver type for configurator resolution.</typeparam>
[CKTypeDefiner]
public abstract class PersistentDomainDriver<TSelf> : IObservableDomainDriver, IRealObject
    where TSelf : PersistentDomainDriver<TSelf>
{
    private readonly string _domainName;
    private readonly bool _autoStartTimer;

    private ObservableDomain _domain;
    private JsonEventCollector _eventCollector;
    private PersistentDomainClient? _client;
    private SnapshotSaver? _saver;
    private FolderLock? _lock;
    private PersistentDomainLoadOutcome? _loadOutcome;

    /// <inheritdoc />
    public string DomainName => _domainName;

    /// <summary>
    /// Gets whether the domain's <see cref="ObservableDomain.TimeManager"/> is automatically started
    /// at the end of the initial configuration transaction.
    /// <para>
    /// The load always stops the timer. When this is false, the timer stays stopped after the start, also when
    /// the snapshot file holds a running timer: the driver does not restore this state.
    /// </para>
    /// </summary>
    public bool AutoStartTimer => _autoStartTimer;

    /// <inheritdoc />
    public ObservableDomain Domain => _domain;

    /// <inheritdoc />
    public JsonEventCollector EventCollector => _eventCollector;

    /// <summary>
    /// Gets the outcome of the load at start. The driver sets it before it calls the load handlers
    /// and <see cref="PreConfigure"/>. It is null before the start.
    /// </summary>
    public PersistentDomainLoadOutcome? LoadOutcome => _loadOutcome;

    /// <summary>
    /// Gets whether this start created the domain: <see cref="LoadOutcome"/> is Created or CreatedAfterLoadFailure.
    /// Available from <see cref="PreConfigure"/> onwards.
    /// </summary>
    public bool IsNewDomain => _loadOutcome?.IsNewDomain ?? false;

    /// <summary>
    /// Gets whether the persistence can save. It becomes false when a snapshot creation in memory throws, or when the
    /// restore after a failed transaction throws: the state in memory then cannot be trusted. The driver logs a fatal
    /// error once, then writes no file until the next start: no save, no retry, no final save at stop.
    /// The next start loads the last good generation. It is true before the start.
    /// </summary>
    public bool IsPersistenceHealthy => _client is not { IsPoisoned: true };

    // The saver of the last start, also after the stop or a failed start. For the tests.
    internal SnapshotSaver? Saver => _saver;

    /// <summary>
    /// Initializes a new <see cref="PersistentDomainDriver{TSelf}"/>.
    /// </summary>
    /// <param name="domainName">
    /// The domain name. When null, defaults to the concrete type's <see cref="Type.FullName"/>.
    /// </param>
    /// <param name="autoStartTimer">True to start the domain's time manager after configuration.</param>
    protected PersistentDomainDriver( string? domainName, bool autoStartTimer )
    {
        _domainName = domainName ?? GetType().FullName!;
        _autoStartTimer = autoStartTimer;
        _domain = null!;
        _eventCollector = null!;
    }

    // The generated host resolves the parameters from a scope, and it disposes the scope at the end of the start.
    // The domain keeps its service provider for its life, so it gets the root provider, not the scope.
    private Task OnHostStartAsync( IActivityMonitor monitor,
                                   PersistentDomainRootServices rootServices,
                                   IEnumerable<IObservableDomainConfigurator<TSelf>> configurators,
                                   IEnumerable<IPersistentDomainLoadHandler<TSelf>> loadHandlers )
    {
        return DoHostStartAsync( monitor, rootServices.Services, configurators, loadHandlers );
    }

    internal async Task DoHostStartAsync( IActivityMonitor monitor,
                                          IServiceProvider serviceProvider,
                                          IEnumerable<IObservableDomainConfigurator<TSelf>> configurators,
                                          IEnumerable<IPersistentDomainLoadHandler<TSelf>> loadHandlers )
    {
        var options = PersistentDomainDriverOptions.Read( (IConfiguration?)serviceProvider.GetService( typeof( IConfiguration ) ), _domainName );
        if( !options.Validate( monitor, _domainName ) )
        {
            throw new InvalidOperationException( $"Domain '{_domainName}': the '{PersistentDomainDriverOptions.SectionName}' configuration section has invalid options. See the logged errors." );
        }
        var folder = ResolveFolder( monitor, serviceProvider, options );
        Directory.CreateDirectory( folder );
        var folderLock = FolderLock.TryAcquire( GenerationStore.GetLockFilePath( folder, _domainName ) );
        if( folderLock == null )
        {
            throw new InvalidOperationException( $"Domain '{_domainName}': another process or driver holds the lock of the folder '{folder}'." );
        }
        _lock = folderLock;
        SnapshotSaver? saver = null;
        try
        {
            // The store reads the folder again under the lock, so its next generation is correct.
            var store = new GenerationStore( folder, _domainName );
            store.DeleteOrphanTempFiles( monitor );
            var result = DomainLoader.Load( monitor, store, options, () => new PersistentDomainClient( _domainName, options, serviceProvider ) );
            var outcome = result.Outcome;
            _loadOutcome = outcome;
            if( outcome.Kind == PersistentDomainLoadKind.Failed )
            {
                // The start fails in all cases: the return value of the handlers has no effect.
                await CallHandlersAsync( monitor, loadHandlers, outcome ).ConfigureAwait( false );
                // The inner exception is the error of the first skipped file that has one. A damaged file has no error.
                throw new InvalidOperationException( $"Domain '{_domainName}': no snapshot file in '{folder}' loads. No file changed. See the logged warnings.",
                                                     outcome.SkippedFiles.Select( s => s.Error ).FirstOrDefault( e => e != null ) );
            }
            _domain = result.Domain!;
            _client = result.Client!;
            if( !await CallHandlersAsync( monitor, loadHandlers, outcome ).ConfigureAwait( false ) )
            {
                throw new InvalidOperationException( $"Domain '{_domainName}': one or more load handlers stopped the start. No file changed. See the logged errors." );
            }

            _eventCollector = new JsonEventCollector( _domain )
            {
                KeepDuration = options.ExportedEventKeepDuration,
                KeepLimit = options.ExportedEventKeepLimit
            };

            // The load stopped the timer: no timed event runs before PreConfigure. The saver is not attached yet,
            // so a failure in this transaction (and its rollback) changes no file. The start does not wait for the
            // domain post actions: one that never ends must not block the start with the lock held.
            await _domain.ModifyThrowAsync( monitor, () =>
            {
                PreConfigure( monitor, _domain );

                if( outcome.IsNewDomain )
                {
                    InitializeNewDomain( monitor, _domain );
                }

                var success = true;
                foreach( var configurator in configurators )
                {
                    success &= configurator.ConfigureDomain( monitor, _domain );
                }

                if( !success )
                    Throw.InvalidOperationException( "One or more domain configurators failed." );

                PostConfigure( monitor, _domain );

                // The last step: the timed events that are due run at the end of this transaction.
                if( AutoStartTimer )
                    _domain.TimeManager.Start();
            }, waitForDomainPostActionsCompletion: false ).ConfigureAwait( false );

            // From here, the start can change files. A commit after this line can save.
            // The saver keeps the client and the domain of this start: a failed start sets the fields to null.
            var client = _client;
            var domain = _domain;
            saver = new SnapshotSaver( store,
                                       options,
                                       m => client.CaptureCommittedSnapshot( m, domain ),
                                       () => client.LastCommittedTransactionNumber,
                                       () => client.IsPoisoned );
            // The first save of this start applies the marks just before the rename of its file.
            // If the start fails before that, no file changes. If a file is written, the marks are on disk too.
            saver.SetLoadMarks( DomainLoader.GetMarks( outcome ) );
            _client.Saver = saver;
            _saver = saver;

            // The first save has no condition: a new domain gets its first generation. A commit of the timer
            // that runs now can save first: the saver writes the newest snapshot once.
            var first = _client.CaptureCommittedSnapshot( monitor, _domain );
            saver.Offer( first );
            await saver.SaveAsync( monitor ).ConfigureAwait( false );
            if( saver.SavedTransactionNumber < first.TransactionSerialNumber )
            {
                throw new InvalidOperationException( $"Domain '{_domainName}': the driver cannot write the snapshot file in '{folder}'. See the logged errors." );
            }
        }
        catch
        {
            await ReleaseAfterFailedStartAsync( monitor, saver, folderLock ).ConfigureAwait( false );
            throw;
        }

        // The start succeeded. The first save applied the marks before the rename of its file.
        // The retention never deletes the file of a failed mark. It never throws.
        saver.RetentionEnabled = true;
        await saver.ApplyRetentionAsync( monitor ).ConfigureAwait( false );
    }

    // After a failed start: the saver closes, then the lock is released, then the domain is disposed, in all cases.
    async Task ReleaseAfterFailedStartAsync( IActivityMonitor monitor, SnapshotSaver? saver, FolderLock folderLock )
    {
        var domain = _domain;
        _domain = null!;
        _lock = null;
        try
        {
            await CloseSaverAsync( monitor, saver ).ConfigureAwait( false );
            _client = null;
        }
        finally
        {
            folderLock.Dispose();
        }
        // The host does not use the domain of a failed start. Its timer must not run.
        if( domain != null ) await DisposeDomainAsync( monitor, domain ).ConfigureAwait( false );
    }

    // No new commit gets the saver. The close waits for a save in progress, and a later save writes nothing.
    // This method never throws: the lock release that follows must run.
    async Task CloseSaverAsync( IActivityMonitor monitor, SnapshotSaver? saver )
    {
        try
        {
            if( _client != null ) _client.Saver = null;
            if( saver != null )
            {
                await saver.CloseAsync().ConfigureAwait( false );
                saver.Dispose();
            }
        }
        catch( Exception ex )
        {
            monitor.Error( $"Domain '{_domainName}': error while the driver closes the saver.", ex );
        }
    }

    // ObservableDomain.Dispose waits for the domain post action that runs: one that never ends blocks it.
    // So the driver disposes the domain on another thread and waits for it at most this time.
    static readonly TimeSpan _domainDisposeWait = TimeSpan.FromSeconds( 2 );

    // The dispose stops the timer and detaches the client before it waits for the domain post actions.
    async Task DisposeDomainAsync( IActivityMonitor monitor, ObservableDomain domain )
    {
        var dispose = Task.Run( () => domain.Dispose() );
        if( await Task.WhenAny( dispose, Task.Delay( _domainDisposeWait ) ).ConfigureAwait( false ) == dispose )
        {
            try
            {
                await dispose.ConfigureAwait( false );
            }
            catch( Exception ex )
            {
                monitor.Error( $"Domain '{_domainName}': error while the driver disposes the domain.", ex );
            }
        }
        else
        {
            monitor.Warn( $"Domain '{_domainName}': the dispose of the domain waits for a domain post action. The driver does not wait for its end." );
            // A late error is not lost in the unobserved task exceptions.
            _ = dispose.ContinueWith( t => t.Exception, TaskContinuationOptions.OnlyOnFaulted );
        }
    }

    // Calls all the handlers, as the configurators run. Logs each refusal and each exception.
    // Returns false if one handler returned false or threw.
    async Task<bool> CallHandlersAsync( IActivityMonitor monitor, IEnumerable<IPersistentDomainLoadHandler<TSelf>> loadHandlers, PersistentDomainLoadOutcome outcome )
    {
        bool success = true;
        foreach( var handler in loadHandlers )
        {
            try
            {
                if( !await handler.OnDomainLoadedAsync( monitor, (TSelf)this, outcome ).ConfigureAwait( false ) )
                {
                    monitor.Error( $"Domain '{_domainName}': the load handler '{handler.GetType().ToCSharpName()}' returned false ({outcome.Kind})." );
                    success = false;
                }
            }
            catch( Exception ex )
            {
                monitor.Error( $"Domain '{_domainName}': the load handler '{handler.GetType().ToCSharpName()}' failed ({outcome.Kind}).", ex );
                success = false;
            }
        }
        return success;
    }

    private Task OnHostStopAsync( IActivityMonitor monitor )
    {
        return DoHostStopAsync( monitor );
    }

    internal async Task DoHostStopAsync( IActivityMonitor monitor )
    {
        var folderLock = _lock;
        if( folderLock == null ) return;
        _lock = null;
        var saver = _saver;
        try
        {
            if( saver != null && _client != null )
            {
                await FinalSaveAsync( monitor, saver, _client ).ConfigureAwait( false );
            }
            await CloseSaverAsync( monitor, saver ).ConfigureAwait( false );
        }
        finally
        {
            // The lock release runs in all cases. No save of this driver can write after it.
            folderLock.Dispose();
        }
        // After the lock release: the dispose can wait for a domain post action.
        await DisposeDomainAsync( monitor, _domain ).ConfigureAwait( false );
    }

    // The final save copies the committed state under the domain read lock. It runs no transaction: a failing
    // TransactionDone handler or a due timed event that rolls back cannot skip it, and watchers get no empty event.
    // If the state is already saved, it writes no file. It still applies the retention. This method never throws.
    async Task FinalSaveAsync( IActivityMonitor monitor, SnapshotSaver saver, PersistentDomainClient client )
    {
        try
        {
            // The saver stops outside any transaction: a timer save needs the domain read lock.
            await saver.StopAsync().ConfigureAwait( false );
            if( client.IsPoisoned )
            {
                monitor.Error( $"Domain '{_domainName}': the state in memory cannot be trusted. The stop writes no file. The next start loads the last good generation." );
                return;
            }
            var snapshot = client.CaptureCommittedSnapshot( monitor, _domain );
            if( snapshot.TransactionSerialNumber > saver.SavedTransactionNumber )
            {
                saver.Offer( snapshot );
                await saver.SaveAsync( monitor ).ConfigureAwait( false );
                if( saver.SavedTransactionNumber < snapshot.TransactionSerialNumber )
                {
                    monitor.Error( $"Domain '{_domainName}': the final save failed. The newest file can miss the last changes. See the logged errors." );
                }
            }
            else
            {
                monitor.Trace( $"Domain '{_domainName}': the state n°{snapshot.TransactionSerialNumber} is already saved. The stop writes no file." );
            }
            await saver.ApplyRetentionAsync( monitor ).ConfigureAwait( false );
        }
        catch( Exception ex )
        {
            monitor.Error( $"Domain '{_domainName}': error during the final save.", ex );
        }
    }

    string ResolveFolder( IActivityMonitor monitor, IServiceProvider services, PersistentDomainOptions options )
    {
        // An empty StorePath is the same as null: a JSON null binds as an empty string.
        if( !string.IsNullOrEmpty( options.StorePath ) ) return options.StorePath;
        if( options.StorePath != null )
        {
            monitor.Info( $"Domain '{_domainName}': the option StorePath is empty. The driver uses the default folder." );
        }
        if( services.GetService( typeof( ApplicationIdentityService ) ) is ApplicationIdentityService identity )
        {
            return identity.LocalFileStore.FolderPath.AppendPart( "ObservableDomains" ).AppendPart( _domainName ).Path;
        }
        throw new InvalidOperationException( $"Domain '{_domainName}': no folder for the snapshot files. The option '{PersistentDomainDriverOptions.SectionName}:Domains:{_domainName}:StorePath' is not set and no ApplicationIdentityService is registered." );
    }

    /// <summary>
    /// Called inside the initial transaction, before any <see cref="IObservableDomainConfigurator{T}"/> runs.
    /// Runs on every host start, against a fresh or a reloaded domain: must be idempotent
    /// (find-or-create semantics, typically <see cref="DomainView.CreateSingleton{T}"/>).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domain">The domain being configured.</param>
    protected virtual void PreConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
    }

    /// <summary>
    /// Called inside the initial transaction, after <see cref="PreConfigure"/> and before any
    /// <see cref="IObservableDomainConfigurator{T}"/> runs, and ONLY when this start created the domain:
    /// a reloaded domain never sees it. Override to seed state from an external source (a database, a
    /// remote party) without overwriting what a previous run persisted. Configurators and
    /// <see cref="PostConfigure"/> then observe the seeded domain.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domain">The domain being configured.</param>
    protected virtual void InitializeNewDomain( IActivityMonitor monitor, ObservableDomain domain )
    {
    }

    /// <summary>
    /// Called inside the initial transaction, after all <see cref="IObservableDomainConfigurator{T}"/> have run.
    /// Runs on every host start, against a fresh or a reloaded domain: must be idempotent.
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
        Throw.CheckState( _domain is not null );
        return _domain.ModifyAsync( monitor,
                                    () => actions.Invoke( monitor, _domain ),
                                    throwException,
                                    millisecondsTimeout,
                                    considerRolledbackAsFailure,
                                    parallelDomainPostActions,
                                    waitForDomainPostActionsCompletion );
    }
}
