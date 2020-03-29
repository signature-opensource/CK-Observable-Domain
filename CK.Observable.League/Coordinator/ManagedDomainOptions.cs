using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Immutable definition of snapshot options: <see cref="CompressionKind"/>, <see cref="SnapshotSaveDelay"/>, <see cref="ExportedEventKeepDuration"/>
    /// and <see cref="ExportedEventKeepLimit"/>.
    /// </summary>
    [SerializationVersion( 0 )]
    public class ManagedDomainOptions : IEquatable<ManagedDomainOptions>
    {
        /// <summary>
        /// The Snapshot compression kind.
        /// </summary>
        public readonly CompressionKind CompressionKind;

        /// <summary>
        /// Minimum time between each save, checked on every transaction commit.
        /// When negative, the file will not be saved automatically (manual save must be done by <see cref="IObservableDomainShell.SaveAsync"/>).
        /// When 0, every transaction will be saved.
        /// </summary>
        public readonly TimeSpan SnapshotSaveDelay;

        /// <summary>
        /// Gets the minimum time span during which snapshot files for this domain must be kept.
        /// Recent snapshots will not be deleted (even if <see cref="MaximumTotalKbToKeep"/> applies).
        /// Setting both this and <see cref="MaximumTotalKbToKeep"/> to 0 suppress any archive cleanup.
        /// Defaults to 2 days.
        /// </summary>
        public TimeSpan SnapshotKeepDuration { get; }

        /// <summary>
        /// Gets the maximum size snapshot files for this domain can use, in Kibibyte.
        /// Snapshot files within <see cref="SnapshotKeepDuration"/> will not be deleted, even if their cumulative
        /// size exceeds this value.
        /// Setting both this and <see cref="MaximumTotalKbToKeep"/> to 0 suppress any file cleanup.
        /// Defaults to 10 Mebibyte.
        /// </summary>
        public int SnapshotMaximalTotalKiB { get; }

        /// <summary>
        /// Gets or sets the maximum time during which events are kept.
        /// Defaults to one hour.
        /// </summary>
        public readonly TimeSpan ExportedEventKeepDuration;

        /// <summary>
        /// Gets or sets the minimum number of transaction events that are kept, regardless of <see cref="KeepDuration"/>.
        /// Default to 100.
        /// </summary>
        public readonly int ExportedEventKeepLimit;

        /// <summary>
        /// Initializes a new <see cref="ManagedDomainOptions"/>.
        /// </summary>
        public ManagedDomainOptions(
            CompressionKind c,
            TimeSpan snapshotSaveDelay,
            TimeSpan snapshotKeepDuration,
            int snapshotMaximalTotalKiB,
            TimeSpan eventKeepDuration,
            int eventKeepLimit)
        {
            CompressionKind = c;
            SnapshotSaveDelay = snapshotSaveDelay;
            SnapshotKeepDuration = snapshotKeepDuration;
            SnapshotMaximalTotalKiB = snapshotMaximalTotalKiB;
            ExportedEventKeepDuration = eventKeepDuration;
            ExportedEventKeepLimit = eventKeepLimit;
        }

        internal ManagedDomainOptions( IBinaryDeserializerContext ctx )
        {
            var r = ctx.StartReading();
            CompressionKind = r.ReadEnum<CompressionKind>();
            SnapshotSaveDelay = r.ReadTimeSpan();
            SnapshotKeepDuration = r.ReadTimeSpan();
            SnapshotMaximalTotalKiB = r.ReadInt32();
            ExportedEventKeepDuration = r.ReadTimeSpan();
            ExportedEventKeepLimit = r.ReadInt32();
        }

        void Write( BinarySerializer w )
        {
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
        public override int GetHashCode() => Util.Hash.Combine( Util.Hash.StartValue, CompressionKind, SnapshotSaveDelay, SnapshotMaximalTotalKiB, SnapshotKeepDuration, ExportedEventKeepDuration, ExportedEventKeepLimit ).GetHashCode();

        /// <summary>
        /// Value semantic equality.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True on equal, false otherwise.</returns>
        public bool Equals( ManagedDomainOptions other ) => ExportedEventKeepLimit == other.ExportedEventKeepLimit
                                                            && ExportedEventKeepDuration == other.ExportedEventKeepDuration
                                                            && SnapshotSaveDelay == other.SnapshotSaveDelay
                                                            && CompressionKind == other.CompressionKind;
    }
}
