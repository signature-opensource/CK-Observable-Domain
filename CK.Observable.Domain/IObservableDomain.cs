using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Exposes the heart of an <see cref="ObservableDomain"/>. This is the only API surface that
/// should be used by ModifyAsync methods action (inside a transaction).
/// </summary>
public interface IObservableDomain
{
    /// <summary>
    /// Gets this domain name.
    /// </summary>
    string DomainName { get; }

    /// <summary>
    /// Gets all the observable objects that this domain contains (roots included).
    /// </summary>
    IObservableAllObjectsCollection AllObjects { get; }

    /// <summary>
    /// Gets all the internal objects that this domain contains.
    /// </summary>
    IReadOnlyCollection<InternalObject> AllInternalObjects { get; }

    /// <summary>
    /// Gets the root observable objects that this domain contains.
    /// </summary>
    IReadOnlyList<ObservableRootObject> AllRoots { get; }

    /// <summary>
    /// Gets the current transaction number.
    /// Incremented each time a transaction successfully ended, default to 0 until the first transaction commit.
    /// </summary>
    int TransactionSerialNumber { get; }

    /// <summary>
    /// Gets the last commit time. Defaults to <see cref="DateTime.UtcNow"/> at the very beginning,
    /// when no transaction has been committed yet (and <see cref="TransactionSerialNumber"/> is 0).
    /// </summary>
    DateTime TransactionCommitTimeUtc { get; }

    /// <summary>
    /// Gets the secret key for this domain. It is a <see cref="System.Security.Cryptography.Rfc2898DeriveBytes"/> bytes
    /// array of length <see cref="ObservableDomain.DomainSecretKeyLength"/> derived from <see cref="Guid.NewGuid()"/>.
    /// </summary>
    ReadOnlySpan<byte> SecretKey { get; }

    /// <summary>
    /// Gets the <see cref="ITimeManager"/> that is in charge of <see cref="ObservableReminder"/>
    /// and <see cref="ObservableTimer"/> objects.
    /// </summary>
    ITimeManager TimeManager { get; }

    /// <summary>
    /// Creates a singleton instance from its type. Even if it is named "Create", this actually is a "find or create".
    /// <para>
    /// Once useless, the object should be destroyed if it has also been created by other participants: its real destruction
    /// will wait until all participants have also destroyed their obtained instance.
    /// </para>
    /// </summary>
    /// <param name="type">
    /// The singleton type.
    /// Must be a <see cref="IObservableDomainSingleton"/> otherwise an <see cref="ArgumentException"/> is thrown.
    /// </param>
    /// <returns>A new or already existing instance.</returns>
    IObservableDomainObject CreateSingleton( Type type );

    /// <summary>
    /// Creates a singleton instance for type <typeparamref name="T"/>. Even if it is named "Create", this actually
    /// is a "find or create".
    /// <para>
    /// Once useless, the object should be destroyed if it has also been created by other participants: its real destruction
    /// will wait until all participants have also destroyed their obtained instance.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of singleton to obtain.</typeparam>
    /// <returns>A new or already existing instance.</returns>
    T CreateSingleton<T>() where T : class, IObservableDomainSingleton;

    /// <summary>
    /// Saves this domain.
    /// </summary>
    /// <param name="monitor">The monitor to use. Cannot be null.</param>
    /// <param name="stream">The output stream. Must be opened and is left opened.</param>
    /// <param name="debugMode">True to activate <see cref="BinarySerializer.IsDebugMode"/>.</param>
    /// <param name="millisecondsTimeout">
    /// The maximum number of milliseconds to wait for a read access before giving up.
    /// Wait indefinitely by default.
    /// </param>
    /// <returns>True on success, false if timeout occurred.</returns>
    bool Save( IActivityMonitor monitor,
               Stream stream,
               bool debugMode = false,
               int millisecondsTimeout = -1 );

}
