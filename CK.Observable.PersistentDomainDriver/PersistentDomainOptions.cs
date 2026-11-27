using System;
using System.Collections.Generic;
using System.IO;
using CK.Core;
using Microsoft.Extensions.Configuration;

namespace CK.Observable;

/// <summary>
/// Options for <see cref="PersistentDomainDriver{TSelf}"/> persistence, bound from
/// the "CK-ObservableDomain" configuration section (same pattern as "CK-ObservableLeague").
/// All options are per domain: the section has no global option.
/// </summary>
public sealed class PersistentDomainDriverOptions
{
    /// <summary>The configuration section name: "CK-ObservableDomain".</summary>
    public const string SectionName = "CK-ObservableDomain";

    /// <summary>Per-domain options, keyed by <see cref="IObservableDomainDriver.DomainName"/>.</summary>
    public Dictionary<string, PersistentDomainOptions> Domains { get; set; } = new Dictionary<string, PersistentDomainOptions>( StringComparer.OrdinalIgnoreCase );

    /// <summary>Gets the options configured for a domain, or default options when the domain has no entry.</summary>
    /// <param name="domainName">The domain name.</param>
    /// <returns>The domain's options (never null).</returns>
    public PersistentDomainOptions GetDomainOptions( string domainName )
    {
        return Domains.TryGetValue( domainName, out var options ) ? options : new PersistentDomainOptions();
    }

    /// <summary>
    /// Reads the options of one domain from the "CK-ObservableDomain:Domains:&lt;DomainName&gt;" section.
    /// The method binds this section directly: a value that does not convert throws.
    /// A bind of the whole <see cref="Domains"/> dictionary drops such an entry with no error,
    /// and the domain then starts with the default options.
    /// </summary>
    /// <param name="configuration">The configuration. Null gives the default options.</param>
    /// <param name="domainName">The domain name. The comparison ignores the case.</param>
    /// <returns>The options of the domain, or the default options when the domain has no entry.</returns>
    /// <exception cref="InvalidOperationException">A value of the entry does not convert.</exception>
    internal static PersistentDomainOptions Read( IConfiguration? configuration, string domainName )
    {
        var options = new PersistentDomainOptions();
        if( configuration == null ) return options;
        foreach( var entry in configuration.GetSection( SectionName ).GetSection( nameof( Domains ) ).GetChildren() )
        {
            if( !StringComparer.OrdinalIgnoreCase.Equals( entry.Key, domainName ) ) continue;
            try
            {
                entry.Bind( options );
            }
            catch( InvalidOperationException ex )
            {
                throw new InvalidOperationException( $"Domain '{domainName}': the configuration section '{entry.Path}' has a value that does not convert. {ex.Message}", ex );
            }
        }
        return options;
    }
}

/// <summary>
/// Snapshot policy for one persistent domain, named after the League's per-domain configuration
/// (CK.Observable.League's EnsureDomainOptions / ManagedDomainOptions) so that a reader who knows one
/// knows the other.
/// <para>
/// Two differences from the League are deliberate. Its names carry a 'Create' prefix because they apply
/// only when a domain is first created and are then persisted in the coordinator; these are re-read and
/// re-applied on EVERY host start, so configuration stays the source of truth. And its per-domain
/// LifeCycleOption and RootTypes have no counterpart here: a driver owns exactly one always-loaded domain
/// whose type it declares itself, so neither has anything to decide.
/// </para>
/// </summary>
public sealed class PersistentDomainOptions
{
    /// <summary>
    /// Folder of this domain's snapshot files. Must be a fully qualified path.
    /// When null (the default) or empty, the driver chooses the folder.
    /// An empty value is the same as null, because a JSON null binds as an empty string.
    /// </summary>
    public string? StorePath { get; set; }

    /// <summary>
    /// Minimal delay between two automatic snapshot file writes:
    /// <see cref="TimeSpan.Zero"/> (the default) saves on every committed transaction, a positive value
    /// debounces the writes, a negative one disables automatic saves (snapshots are then only written by
    /// a transaction that calls <see cref="ObservableDomain.SendSnapshotCommand"/>, and by the graceful host stop).
    /// Must not be longer than 1 day.
    /// </summary>
    public TimeSpan SnapshotSaveDelay { get; set; }

    /// <summary>Number of snapshot generations that the store keeps. Defaults to 5. Must be 1 or more.</summary>
    public int GenerationCount { get; set; } = 5;

    /// <summary>
    /// Number of older generations that a load can try when the newest generation is damaged.
    /// Defaults to 0 (no fallback). Must be 0 or more and lower than <see cref="GenerationCount"/>.
    /// </summary>
    public int FallbackGenerationCount { get; set; }

    /// <summary>
    /// How long the store keeps a ".damaged" or ".superseded" snapshot file before it deletes the file.
    /// The time starts at the rename. Defaults to 7 days. Must be between zero and 3650 days.
    /// </summary>
    public TimeSpan DamagedFileKeepDuration { get; set; } = TimeSpan.FromDays( 7 );

    /// <summary>Compression of the snapshot stream. Defaults to <see cref="CompressionKind.None"/>.</summary>
    public CompressionKind CompressionKind { get; set; }

    /// <summary>See <see cref="MemoryTransactionProviderClient.SkipTransactionCount"/>. Defaults to 0 (full transacted mode). Must be -1 or more.</summary>
    public int SkipTransactionCount { get; set; }

    /// <summary>
    /// Maximal time during which exported transaction events are kept for watchers. Defaults to 5 minutes.
    /// Must not be negative and must not be longer than 1 day.
    /// </summary>
    public TimeSpan ExportedEventKeepDuration { get; set; } = TimeSpan.FromMinutes( 5 );

    /// <summary>Minimal number of exported transaction events kept regardless of <see cref="ExportedEventKeepDuration"/>. Defaults to 10, the minimum is 1.</summary>
    public int ExportedEventKeepLimit { get; set; } = 10;

    /// <summary>
    /// Whether sentinel bytes are written when serializing, and verified when deserializing.
    /// Defaults to false. See <see cref="MemoryTransactionProviderClient.DebugMode"/>.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Whether the driver creates a new empty domain when no allowed snapshot file loads.
    /// Defaults to false: then the start fails, because the state of a persistent domain is worth more than
    /// availability. Set it to true for a domain that an external source can rebuild.
    /// <para>
    /// With true, the outcome is <see cref="PersistentDomainLoadKind.CreatedAfterLoadFailure"/>. The driver renames the
    /// files that it tried to ".damaged" or ".unloadable", and the files that it did not try to ".superseded".
    /// The retention deletes ".damaged" and ".superseded" files after <see cref="DamagedFileKeepDuration"/>,
    /// and never deletes ".unloadable" files. A read error fails the start whatever this option.
    /// </para>
    /// <para>
    /// This is the League's per-domain 'createOnLoadError' choice: it uses false for the coordinator and
    /// true for the domains it manages.
    /// </para>
    /// </summary>
    public bool CreateOnLoadError { get; set; }

    /// <summary>
    /// The longest valid <see cref="SnapshotSaveDelay"/>. Each commit adds the delay to its commit time,
    /// and the save timer throws for a delay above about 49.7 days.
    /// </summary>
    internal static readonly TimeSpan MaxSnapshotSaveDelay = TimeSpan.FromDays( 1 );

    /// <summary>
    /// The longest valid <see cref="ExportedEventKeepDuration"/>. The event collector subtracts it from the current time
    /// at each transaction: a huge value gives a date before the first valid date and throws.
    /// </summary>
    internal static readonly TimeSpan MaxExportedEventKeepDuration = TimeSpan.FromDays( 1 );

    /// <summary>The longest valid <see cref="DamagedFileKeepDuration"/>. A longer value overflows a date computation.</summary>
    internal static readonly TimeSpan MaxDamagedFileKeepDuration = TimeSpan.FromDays( 3650 );

    /// <summary>
    /// Checks the options of one domain. Logs one error per invalid option.
    /// </summary>
    /// <param name="monitor">The monitor that receives the errors.</param>
    /// <param name="domainName">The domain name, used in the error messages.</param>
    /// <returns>True if all options are valid, false if at least one is invalid.</returns>
    internal bool Validate( IActivityMonitor monitor, string domainName )
    {
        bool valid = true;
        if( GenerationCount < 1 )
        {
            monitor.Error( $"Domain '{domainName}': option GenerationCount is {GenerationCount}. It must be 1 or more." );
            valid = false;
        }
        if( FallbackGenerationCount < 0 )
        {
            monitor.Error( $"Domain '{domainName}': option FallbackGenerationCount is {FallbackGenerationCount}. It must be 0 or more." );
            valid = false;
        }
        else if( FallbackGenerationCount >= GenerationCount )
        {
            monitor.Error( $"Domain '{domainName}': option FallbackGenerationCount is {FallbackGenerationCount}. It must be lower than GenerationCount ({GenerationCount})." );
            valid = false;
        }
        // An empty StorePath is the same as null: a JSON null binds as an empty string.
        if( !string.IsNullOrEmpty( StorePath ) && !Path.IsPathFullyQualified( StorePath ) )
        {
            monitor.Error( $"Domain '{domainName}': option StorePath is '{StorePath}'. It must be a fully qualified path." );
            valid = false;
        }
        // The binder converts a number to the enum with no check. With an undefined value, the client writes
        // a compressed header and an uncompressed payload, and no later start can load the file.
        if( !Enum.IsDefined( CompressionKind ) )
        {
            monitor.Error( $"Domain '{domainName}': option CompressionKind is {(int)CompressionKind}. It must be None or GZiped." );
            valid = false;
        }
        // The next three limits are the limits of the setters of MemoryTransactionProviderClient and JsonEventCollector.
        if( SkipTransactionCount < -1 )
        {
            monitor.Error( $"Domain '{domainName}': option SkipTransactionCount is {SkipTransactionCount}. It must be -1 or more." );
            valid = false;
        }
        if( ExportedEventKeepDuration < TimeSpan.Zero )
        {
            monitor.Error( $"Domain '{domainName}': option ExportedEventKeepDuration is {ExportedEventKeepDuration}. It must not be negative." );
            valid = false;
        }
        else if( ExportedEventKeepDuration > MaxExportedEventKeepDuration )
        {
            monitor.Error( $"Domain '{domainName}': option ExportedEventKeepDuration is {ExportedEventKeepDuration}. It must not be longer than {MaxExportedEventKeepDuration.TotalDays} day." );
            valid = false;
        }
        if( ExportedEventKeepLimit < 1 )
        {
            monitor.Error( $"Domain '{domainName}': option ExportedEventKeepLimit is {ExportedEventKeepLimit}. It must be 1 or more." );
            valid = false;
        }
        if( SnapshotSaveDelay > MaxSnapshotSaveDelay )
        {
            monitor.Error( $"Domain '{domainName}': option SnapshotSaveDelay is {SnapshotSaveDelay}. It must not be longer than {MaxSnapshotSaveDelay.TotalDays} day." );
            valid = false;
        }
        if( DamagedFileKeepDuration < TimeSpan.Zero )
        {
            monitor.Error( $"Domain '{domainName}': option DamagedFileKeepDuration is {DamagedFileKeepDuration}. It must not be negative." );
            valid = false;
        }
        else if( DamagedFileKeepDuration > MaxDamagedFileKeepDuration )
        {
            monitor.Error( $"Domain '{domainName}': option DamagedFileKeepDuration is {DamagedFileKeepDuration}. It must not be longer than {MaxDamagedFileKeepDuration.TotalDays} days." );
            valid = false;
        }
        return valid;
    }
}
