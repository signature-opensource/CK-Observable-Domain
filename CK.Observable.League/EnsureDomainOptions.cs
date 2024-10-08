using System;
using System.Collections.Generic;

namespace CK.Observable.League;

/// <summary>
/// Models the default domain options of <see cref="DefaultObservableLeagueOptions.EnsureDomains"/>.
/// </summary>
public sealed class EnsureDomainOptions
{
    /// <summary>
    /// Gets or sets the domain name.
    /// This is a required non empty string. 
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Gets a mutable list of root type assembly qualified names.
    /// If a <see cref="DomainName"/> is found with roots that starts with these ones,
    /// it is kept unchanged: an empty list here preserves any existing domain.
    /// </summary>
    public List<string> RootTypes { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the <see cref="DomainLifeCycleOption"/>.
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public DomainLifeCycleOption CreateLifeCycleOption { get; set; }

    /// <summary>
    /// Gets or sets the Snapshot compression kind.
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public CompressionKind CreateCompressionKind { get; set; }

    /// <summary>
    /// Gets or sets the number of transactions to skip after every commit.
    /// <para>
    /// Defaults to zero: transaction mode is on, unhandled errors trigger a rollback to the previous state (before the commit).
    /// </para>
    /// <para>
    /// When positive, the transaction mode is in a very dangerous mode since the domain may rollback to an old version of itself.
    /// </para>
    /// <para>
    /// When set to -1, transaction mode is off. Unhandled errors are logged (as <see cref="LogLevel.Error"/>) and
    /// silently swallowed by <see cref="MemoryTransactionProviderClient.OnUnhandledException"/> method.
    /// </para>
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public int CreateSkipTransactionCount { get; set; }

    /// <summary>
    /// Minimum time between each save, checked on every transaction commit (used if the domain must be created).
    /// When negative, the file will not be saved automatically (manual save must be done by <see cref="IObservableDomainShellBase.SaveAsync(IActivityMonitor)"/>
    /// or by sending the <see cref="ObservableDomain.SnapshotDomainCommand"/> from a transaction).
    /// When 0 (the default), every transaction will be saved.
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public TimeSpan CreateSnapshotSaveDelay { get; set; }

    /// <summary>
    /// Gets the minimum time span during which snapshot files for this domain must be kept.
    /// Recent snapshots will not be deleted (even if <see cref="SnapshotMaximalTotalKiB"/> applies).
    /// Setting both this and SnapshotMaximalTotalKiB to 0 suppress any archive cleanup.
    /// Defaults to 2 days.
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public TimeSpan CreateSnapshotKeepDuration { get; set; } = TimeSpan.FromDays( 2 );

    /// <summary>
    /// Gets the maximum size snapshot files for this domain can use, in Kibibyte.
    /// Snapshot files within <see cref="SnapshotKeepDuration"/> will not be deleted, even if their cumulative
    /// size exceeds this value.
    /// Setting both this and SnapshotKeepDuration to 0 suppress any file cleanup.
    /// Defaults to 10 Mebibyte.
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public int CreateSnapshotMaximalTotalKiB { get; set; } = 10 * 1024;

    /// <summary>
    /// The rate at which housekeeping is executed, in Modify cycles (ie. how many transactions between housekeeping).
    /// <para>
    /// Defaults to 50.
    /// </para>
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    /// <remarks>Housekeeping is always executed on domain load, and on manual save.</remarks>
    public int CreateHousekeepingRate { get; set; } = 50;

    /// <summary>
    /// Gets the maximum time during which events are kept.
    /// Defaults to 5 minutes.
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    /// </summary>
    public TimeSpan CreateExportedEventKeepDuration { get; set; } = TimeSpan.FromMinutes( 5 );

    /// <summary>
    /// Gets the minimum number of transaction events that are kept, regardless of <see cref="ExportedEventKeepDuration"/>.
    /// Defaults to 10, the minimum is 1.
    /// </summary>
    /// <para>
    /// This is used only if and when the domain must be created.
    /// </para>
    public int CreateExportedEventKeepLimit { get; set; } = 10;
}
