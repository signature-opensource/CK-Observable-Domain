using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Defines the snapshot options: <see cref="CompressionKind"/>, <see cref="AutoSaveTime"/>, <see cref="ExportedEventKeepDuration"/>
    /// and <see cref="ExportedEventKeepLimit"/>.
    /// </summary>
    public readonly struct ManagedDomainOptions : IEquatable<ManagedDomainOptions>
    {
        /// <summary>
        /// The Snapshot compression kind.
        /// </summary>
        public readonly CompressionKind CompressionKind;

        /// <summary>
        /// Minimum number of milliseconds between each save, checked on every transaction commit.
        /// When -1, the file will not be saved automatically (manual save must be done by <see cref="IObservableDomainLoader.SaveAsync"/>).
        /// When 0, every transaction will be saved.
        /// </summary>
        public readonly int AutoSaveTime;

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
        public ManagedDomainOptions( CompressionKind c, int autoSaveTime, TimeSpan eventKeepDuration, int eventKeepLimit)
        {
            if( autoSaveTime < -1 ) throw new ArgumentOutOfRangeException( nameof( autoSaveTime ) );
            CompressionKind = c;
            AutoSaveTime = autoSaveTime;
            ExportedEventKeepDuration = eventKeepDuration;
            ExportedEventKeepLimit = eventKeepLimit;
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
        public override int GetHashCode() => Util.Hash.Combine( Util.Hash.StartValue, AutoSaveTime, CompressionKind, ExportedEventKeepDuration, ExportedEventKeepLimit ).GetHashCode();

        /// <summary>
        /// Value semantic equality.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True on equal, false otherwise.</returns>
        public bool Equals( ManagedDomainOptions other ) => ExportedEventKeepLimit == other.ExportedEventKeepLimit
                                                            && ExportedEventKeepDuration == other.ExportedEventKeepDuration
                                                            && AutoSaveTime == other.AutoSaveTime
                                                            && CompressionKind == other.CompressionKind;
    }
}
