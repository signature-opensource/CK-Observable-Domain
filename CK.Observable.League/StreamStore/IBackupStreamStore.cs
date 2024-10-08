using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// An <see cref="IStreamStore"/> that backs up snapshots on save,
/// and provides methods to manage those backups.
/// </summary>
public interface IBackupStreamStore : IStreamStore
{
    /// <summary>
    /// Cleans backups of the given resource, matching all of the valid given criteria.
    /// </summary>
    /// <param name="monitor">The monitor to use to log deleted files.</param>
    /// <param name="name">The name of the resource for which backups should be cleaned up (case insensitive).</param>
    /// <param name="maximumKeepDuration">
    ///     The maximum age that backups can have. Backups older than the given value are deleted.
    ///     A value of <see cref="TimeSpan.Zero"/> disables deletion by age.
    /// </param>
    /// <param name="maximumTotalBytes">
    ///     The maximum file size that the sum of all backups can have. Backups exceeding this size are deleted, oldest first.
    ///     A value of 0 disables deletion by total size.
    /// </param>
    public void CleanBackups( IActivityMonitor monitor, string name, TimeSpan maximumKeepDuration, long maximumTotalBytes );

    /// <summary>
    /// Gets the names of existing automatic backups performed
    /// for the given resource by this <see cref="IBackupStreamStore"/>.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A collection of existing backup names.</returns>
    IReadOnlyCollection<string> GetBackupNames( string name );
}
