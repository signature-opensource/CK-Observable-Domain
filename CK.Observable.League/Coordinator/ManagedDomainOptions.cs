using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Immutable definition of options for domains managed in a <see cref="ObservableLeague"/>.
    /// </summary>
    [SerializationVersion( 0 )]
    public sealed class ManagedDomainOptions : IEquatable<ManagedDomainOptions>
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
        /// Minimum time between each save, checked on every transaction commit.
        /// When negative, the file will not be saved automatically (manual save must be done by <see cref="IObservableDomainShellBase.SaveAsync(IActivityMonitor)"/>).
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
        /// Gets or sets the maximum time during which events are kept.
        /// Defaults to 5 minutes.
        /// </summary>
        public readonly TimeSpan ExportedEventKeepDuration;

        /// <summary>
        /// Gets or sets the minimum number of transaction events that are kept, regardless of <see cref="ExportedEventKeepDuration"/>.
        /// Defaults to 10, the minimum is 1.
        /// </summary>
        public readonly int ExportedEventKeepLimit;

        /// <summary>
        /// Returns a new immutable option with an updated <see cref="LifeCycleOption"/>.
        /// </summary>
        /// <param name="loadOption">The load option.</param>
        /// <returns>This or a new option instance.</returns>
        public ManagedDomainOptions SetLoadOption( DomainLifeCycleOption loadOption ) => LifeCycleOption == loadOption ? this : new ManagedDomainOptions
            (
                loadOption,
                CompressionKind,
                SnapshotSaveDelay,
                SnapshotKeepDuration,
                SnapshotMaximalTotalKiB,
                ExportedEventKeepDuration,
                ExportedEventKeepLimit 
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
                SnapshotSaveDelay,
                SnapshotKeepDuration,
                SnapshotMaximalTotalKiB,
                ExportedEventKeepDuration,
                ExportedEventKeepLimit 
            );


        /// <summary>
        /// Initializes a new <see cref="ManagedDomainOptions"/>.
        /// </summary>
        public ManagedDomainOptions(
            DomainLifeCycleOption loadOption,
            CompressionKind c,
            TimeSpan snapshotSaveDelay,
            TimeSpan snapshotKeepDuration,
            int snapshotMaximalTotalKiB,
            TimeSpan eventKeepDuration,
            int eventKeepLimit)
        {
            LifeCycleOption = loadOption;
            CompressionKind = c;
            SnapshotSaveDelay = snapshotSaveDelay;
            SnapshotKeepDuration = snapshotKeepDuration;
            SnapshotMaximalTotalKiB = snapshotMaximalTotalKiB;
            ExportedEventKeepDuration = eventKeepDuration;
            ExportedEventKeepLimit = eventKeepLimit;
        }

        ManagedDomainOptions( IBinaryDeserializerContext ctx )
        {
            var r = ctx.StartReading().Reader;
            LifeCycleOption = r.ReadEnum<DomainLifeCycleOption>();
            CompressionKind = r.ReadEnum<CompressionKind>();
            SnapshotSaveDelay = r.ReadTimeSpan();
            SnapshotKeepDuration = r.ReadTimeSpan();
            SnapshotMaximalTotalKiB = r.ReadInt32();
            ExportedEventKeepDuration = r.ReadTimeSpan();
            ExportedEventKeepLimit = r.ReadInt32();
        }

        void Write( BinarySerializer w )
        {
            w.WriteEnum( LifeCycleOption );
            w.WriteEnum( CompressionKind );
            w.Write( SnapshotSaveDelay );
            w.Write( SnapshotKeepDuration );
            w.Write( SnapshotMaximalTotalKiB );
            w.Write( ExportedEventKeepDuration );
            w.Write( ExportedEventKeepLimit );
        }

        /// <summary>
        /// Value semantic equality.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True on equal, false otherwise.</returns>
        public override bool Equals( object obj ) => obj is ManagedDomainOptions o && Equals( o );

        /// <summary>
        /// Value semantic hash code.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => HashCode.Combine( LifeCycleOption, CompressionKind, SnapshotSaveDelay, SnapshotKeepDuration, SnapshotMaximalTotalKiB, ExportedEventKeepDuration, ExportedEventKeepLimit );

        /// <summary>
        /// Value semantic equality.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns>True on equal, false otherwise.</returns>
        public bool Equals( ManagedDomainOptions other ) => LifeCycleOption == other.LifeCycleOption
                                                            && CompressionKind == other.CompressionKind
                                                            && SnapshotSaveDelay == other.SnapshotSaveDelay
                                                            && SnapshotKeepDuration == other.SnapshotKeepDuration
                                                            && SnapshotMaximalTotalKiB == other.SnapshotMaximalTotalKiB
                                                            && ExportedEventKeepDuration == other.ExportedEventKeepDuration
                                                            && ExportedEventKeepLimit == other.ExportedEventKeepLimit;
    }
}
