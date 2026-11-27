using System;
using System.IO;
using System.Threading;

namespace CK.Observable;

/// <summary>
/// A short bounded retry for file operations that another process can block for a short time.
/// For example, an antivirus or an indexer opens a new file without <see cref="FileShare.Delete"/>.
/// <para>
/// The helper makes 5 tries. It waits 20, 40, 80 and 160 ms between them (300 ms in total).
/// It retries only a sharing violation (HResult 0x80070020) and an access denied error (HResult 0x80070005).
/// .NET reports an access denied error as an <see cref="UnauthorizedAccessException"/>.
/// After the last try, the error goes to the caller.
/// </para>
/// </summary>
internal static class TransientFileRetry
{
    const int _sharingViolation = unchecked( (int)0x80070020 );
    const int _accessDenied = unchecked( (int)0x80070005 );

    static readonly int[] _waits = { 20, 40, 80, 160 };

    /// <summary>
    /// Gets whether the exception is a sharing violation or an access denied error.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>True if a later try can succeed.</returns>
    public static bool IsTransient( Exception ex )
    {
        return ex is IOException or UnauthorizedAccessException
               && (ex.HResult == _sharingViolation || ex.HResult == _accessDenied);
    }

    /// <summary>
    /// Runs the action. If it throws a transient error, the method waits and runs it again, at most 5 times in total.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="onRetry">Optional. Called before each wait with the error and the wait in milliseconds.</param>
    public static void Run( Action action, Action<Exception, int>? onRetry = null )
    {
        Run( () => { action(); return 0; }, onRetry );
    }

    /// <summary>
    /// Runs the function. If it throws a transient error, the method waits and runs it again, at most 5 times in total.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="function">The function to run.</param>
    /// <param name="onRetry">Optional. Called before each wait with the error and the wait in milliseconds.</param>
    /// <returns>The result of the function.</returns>
    public static T Run<T>( Func<T> function, Action<Exception, int>? onRetry = null )
    {
        for( int i = 0; ; ++i )
        {
            try
            {
                return function();
            }
            catch( Exception ex ) when( i < _waits.Length && IsTransient( ex ) )
            {
                onRetry?.Invoke( ex, _waits[i] );
                Thread.Sleep( _waits[i] );
            }
        }
    }
}
