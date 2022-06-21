using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Immutable definition of options for domains managed in a <see cref="ObservableLeague"/>.
    /// </summary>
    [SerializationVersion( 3 )]
    public sealed class ManagedDomainOptions : IEquatable<ManagedDomainOptions>, ICKSlicedSerializable
    {
        /// <summary>
        /// Gets the <see cref="DomainLifeCycleOption"/> configuration.
        /// </summary>
        public readonly DomainLifeCycleOption LifeCycleOption;

        /// <summary>
        /// The Snapshot compression kind.
        /// </summary>
        public readonly CompressionKind CompressionKind;

        /// <summary>
        /// Number of transactions to skip after every commit.
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
        /// </summary>
        public readonly int SkipTransactionCount;

        /// <summary>
        /// Minimum time between each save, checked on every transaction commit.
        /// When negative, the file will not be saved automatically (manual save must be done by <see cref="IObservableDomainShellBase.SaveAsync(IActivityMonitor)"/>
        /// or by sending the <see cref="ObservableDomain.SnapshotDomainCommand"/> from a transaction).
        /// When 0, every transaction will be saved.
        /// </summary>
        public readonly TimeSpan SnapshotSaveDelay;

        /// <summary>
        /// Gets the minimum time span during which snapshot files for this domain must be kept.
        /// Recent snapshots will not be deleted (even if <see cref="SnapshotMaximalTotalKiB"/> applies).
        /// Setting both this and SnapshotMaximalTotalKiB to 0 suppress any archive cleanup.
        /// Defaults to 2 days.
        /// </summary>
        public readonly TimeSpan SnapshotKeepDuration;

        /// <summary>
        /// Gets the maximum size snapshot files for this domain can use, in Kibibyte.
        /// Snapshot files within <see cref="SnapshotKeepDuration"/> will not be deleted, even if their cumulative
        /// size exceeds this value.
        /// Setting both this and SnapshotKeepDuration to 0 suppress any file cleanup.
        /// Defaults to 10 Mebibyte.
        /// </summary>
        public readonly int SnapshotMaximalTotalKiB;

        /// <summary>
        /// The rate at which housekeeping is executed, in Modify cycles (ie. how many transactions between housekeeping).
        /// <para>
        /// Defaults to 50.
        /// </para>
        /// </summary>
        /// <remarks>Housekeeping is always executed on domain load, and on manual save.</remarks>
        public readonly int HousekeepingRate;

        /// <summary>
        /// Gets the maximum time during which events are kept.
        /// Defaults to 5 minutes.
        /// </summary>
        public readonly TimeSpan ExportedEventKeepDuration;

        /// <summary>
        /// Gets the minimum number of transaction events that are kept, regardless of <see cref="ExportedEventKeepDuration"/>.
        /// Defaults to 10, the minimum is 1.
        /// </summary>
        public readonly int ExportedEventKeepLimit;

        /// <summary>
        /// Returns a new immutable option with an updated <see cref="LifeCycleOption"/>.
        /// </summary>
        /// <param name="lifeCycleOption">The life cycle option.</param>
        /// <returns>This or a new option instance.</returns>
        public ManagedDomainOptions SetLifeCycleOption( DomainLifeCycleOption lifeCycleOption ) => LifeCycleOption == lifeCycleOption ? this : new ManagedDomainOptions
            (
                lifeCycleOption,
                CompressionKind,
                SkipTransactionCount,
                SnapshotSaveDelay,
                SnapshotKeepDuration,
                SnapshotMaximalTotalKiB,
                ExportedEventKeepDuration,
                ExportedEventKeepLimit,
                HousekeepingRate
            );

        /// <summary>
        /// Returns a new immutable option with an updated <see cref="CompressionKind"/>.
        /// </summary>
        /// <param name="k">The compression kind.</param>
        /// <returns>This or a new option instance.</returns>
        public ManagedDomainOptions SetCompressionKind( CompressionKind k ) => CompressionKind == k ? this : new ManagedDomainOptions
            (
                LifeCycleOption,
                k,
                SkipTransactionCount,
                SnapshotSaveDelay,
                SnapshotKeepDuration,
                SnapshotMaximalTotalKiB,
                ExportedEventKeepDuration,
                ExportedEventKeepLimit,
                HousekeepingRate
            );


        /// <summary>
        /// Initializes a new <see cref="ManagedDomainOptions"/>.
        /// </summary>
        public ManagedDomainOptions( DomainLifeCycleOption loadOption,
                                     CompressionKind c,
                                     int skipTransactionCount,
                                     TimeSpan snapshotSaveDelay,
                                     TimeSpan snapshotKeepDuration,
                                     int snapshotMaximalTotalKiB,
                                     TimeSpan eventKeepDuration,
                                     int eventKeepLimit,
                                     int housekeepingRate )
        {
            LifeCycleOption = loadOption;
            CompressionKind = c;
            SkipTransactionCount = skipTransactionCount;
            SnapshotSaveDelay = snapshotSaveDelay;
            SnapshotKeepDuration = snapshotKeepDuration;
            SnapshotMaximalTotalKiB = snapshotMaximalTotalKiB;
            ExportedEventKeepDuration = eventKeepDuration;
            ExportedEventKeepLimit = eventKeepLimit;
            HousekeepingRate = housekeepingRate;
        }

        ManagedDomainOptions( IBinaryDeserializer d, ITypeReadInfo info )
        {
            var r = d.Reader;
            LifeCycleOption = (DomainLifeCycleOption)r.ReadInt32();
            CompressionKind = (CompressionKind)r.ReadInt32();
            SnapshotSaveDelay = r.ReadTimeSpan();
            SnapshotKeepDuration = r.ReadTimeSpan();
            SnapshotMaximalTotalKiB = r.ReadInt32();
            ExportedEventKeepDuration = r.ReadTimeSpan();
            ExportedEventKeepLimit = r.ReadInt32();
            SkipTransactionCount = r.ReadInt32();
            HousekeepingRate = r.ReadInt32();
        }

        public static void Write( IBinarySerializer s, in ManagedDomainOptions o )
        {
            var w = s.Writer;
            w.Write( (int)o.LifeCycleOption );
            w.Write( (int)o.CompressionKind );
            w.Write( o.SnapshotSaveDelay );
            w.Write( o.SnapshotKeepDuration );
            w.Write( o.SnapshotMaximalTotalKiB );
            w.Write( o.ExportedEventKeepDuration );
            w.Write( o.ExportedEventKeepLimit );
            w.Write( o.SkipTransactionCount );
            w.Write( o.HousekeepingRate );
        }

        /// <summary>
        /// Value semantic equality.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True on equal, false otherwise.</returns>
        public override bool Equals( object? obj ) => obj is ManagedDomainOptions o && Equals( o );

        /// <summary>
        /// Value semantic hash code.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => HashCode.Combine( LifeCycleOption,
                                                               CompressionKind,
                                                               SnapshotSaveDelay,
                                                               SnapshotKeepDuration,
                                                               SnapshotMaximalTotalKiB,
                                                               ExportedEventKeepDuration,
                                                               ExportedEventKeepLimit + SkipTransactionCount );

        /// <summary>
        /// Value semantic equality.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns>True on equal, false otherwise.</returns>
        public bool Equals( ManagedDomainOptions? other ) => other != null
                                                             && LifeCycleOption == other.LifeCycleOption
                                                             && CompressionKind == other.CompressionKind
                                                             && SnapshotSaveDelay == other.SnapshotSaveDelay
                                                             && SkipTransactionCount == other.SkipTransactionCount
                                                             && SnapshotKeepDuration == other.SnapshotKeepDuration
                                                             && SnapshotMaximalTotalKiB == other.SnapshotMaximalTotalKiB
                                                             && ExportedEventKeepDuration == other.ExportedEventKeepDuration
                                                             && ExportedEventKeepLimit == other.ExportedEventKeepLimit
                                                             && HousekeepingRate == other.HousekeepingRate;
    }
}
