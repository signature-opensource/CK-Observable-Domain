using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CK.Observable;

/// <summary>
/// An exclusive lock that lets one process at a time own a domain folder.
/// The operating system holds the lock on an open file handle. The system releases the lock
/// when <see cref="Dispose"/> runs and when the process dies for any reason.
/// <para>
/// Adapted from <c>FileLock.TryAcquire</c> in <c>CK.AppIdentity.Abstractions/FileLock.cs</c>
/// and from <c>UnixFlock.cs</c> in <c>CK.AppIdentity.Configuration</c> (AppIdentity 2.0).
/// </para>
/// <para>
/// On Windows, the lock is the file opened with <see cref="FileShare.None"/>.
/// On Unix, the lock is a direct <c>flock(LOCK_EX)</c>. It does not depend on the .NET
/// <see cref="FileShare"/> emulation, so it holds when that emulation is off.
/// If the file system does not support the lock, <see cref="TryAcquire"/> throws.
/// It never lets the caller run without the lock.
/// </para>
/// <para>
/// The code never deletes the lock file. On Unix, a delete and a new create can let
/// two processes each hold a lock on a different file.
/// </para>
/// </summary>
internal sealed class FolderLock : IDisposable
{
    const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    readonly FileStream _file;

    FolderLock( FileStream file )
    {
        _file = file;
    }

    /// <summary>
    /// Tries to take the lock without waiting. The method creates the lock file if it does not exist.
    /// The folder of the file must exist.
    /// </summary>
    /// <param name="lockFilePath">The path of the lock file.</param>
    /// <returns>The lock, or null if another handle holds the lock.</returns>
    /// <exception cref="IOException">The file system does not support the lock.</exception>
    public static FolderLock? TryAcquire( string lockFilePath )
    {
        ArgumentException.ThrowIfNullOrEmpty( lockFilePath );
        FileStream file;
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.Read, Share = FileShare.None };
            if( !OperatingSystem.IsWindows() ) options.UnixCreateMode = OwnerOnly;
            file = new FileStream( lockFilePath, options );
        }
        catch( IOException ex ) when( IsHeldBySomeoneElse( ex ) )
        {
            return null;
        }
        if( OperatingSystem.IsWindows() ) return new FolderLock( file );
        try
        {
            if( UnixLock( file.SafeFileHandle, lockFilePath ) ) return new FolderLock( file );
        }
        catch
        {
            file.Dispose();
            throw;
        }
        file.Dispose();
        return null;
    }

    /// <summary>
    /// Releases the lock. The lock file stays.
    /// </summary>
    public void Dispose() => _file.Dispose();

    static bool IsHeldBySomeoneElse( IOException ex )
    {
        if( OperatingSystem.IsWindows() )
        {
            // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION.
            int code = ex.HResult & 0xFFFF;
            return code == 32 || code == 33;
        }
        // When the .NET FileShare emulation is on, it takes a flock(LOCK_EX | LOCK_NB) on open.
        // It reports EWOULDBLOCK as an IOException whose HResult is the raw errno.
        // Other plain IOException errors (EMFILE, EIO, ENOSPC, EROFS) are real errors, not "busy".
        return ex.GetType() == typeof( IOException ) && ex.HResult == UnixFlock.WouldBlock;
    }

    [UnsupportedOSPlatform( "windows" )]
    static bool UnixLock( SafeFileHandle handle, string path )
    {
        // When the .NET emulation is on, it already holds LOCK_EX on this descriptor and this call changes nothing.
        // When the emulation is off, this call is the lock.
        switch( UnixFlock.TryLockExclusive( handle, out int errno ) )
        {
            case UnixFlock.Result.Locked: return true;
            case UnixFlock.Result.Busy: return false;
            default:
                throw new IOException( $"Unable to lock '{path}' (errno {errno}): its file system does not support file locking. The lock file must be on a local file system." );
        }
    }

    /// <summary>
    /// Direct <c>flock(2)</c> on an open file.
    /// </summary>
    [UnsupportedOSPlatform( "windows" )]
    static class UnixFlock
    {
        const int LOCK_EX = 2;
        const int LOCK_NB = 4;
        const int EINTR = 4;
        static readonly int EWOULDBLOCK = OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() ? 11 : 35;

        public static int WouldBlock => EWOULDBLOCK;

        public enum Result
        {
            /// <summary>This descriptor holds the lock.</summary>
            Locked,

            /// <summary>Another descriptor holds the lock.</summary>
            Busy,

            /// <summary>The file system does not support file locking.</summary>
            NotSupported
        }

        public static Result TryLockExclusive( SafeFileHandle handle, out int errno )
        {
            bool added = false;
            handle.DangerousAddRef( ref added );
            try
            {
                int fd = (int)handle.DangerousGetHandle();
                for(; ; )
                {
                    if( flock( fd, LOCK_EX | LOCK_NB ) == 0 )
                    {
                        errno = 0;
                        return Result.Locked;
                    }
                    errno = Marshal.GetLastPInvokeError();
                    if( errno == EINTR ) continue;
                    return errno == EWOULDBLOCK ? Result.Busy : Result.NotSupported;
                }
            }
            finally
            {
                if( added ) handle.DangerousRelease();
            }
        }

        [DllImport( "libc", SetLastError = true )]
        static extern int flock( int fd, int operation );
    }
}
