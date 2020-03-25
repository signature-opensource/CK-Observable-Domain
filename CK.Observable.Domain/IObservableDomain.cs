using System;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Exposes the heart of an <see cref="ObservableDomain"/>. This is the only API surface that
    /// should be used by <see cref="ObservableDomain.Modify(Core.IActivityMonitor, Action, int)"/> action (inside
    /// a transaction).
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
        IObservableObjectCollection AllObjects { get; }

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
        /// Incremented each time a transaction successfuly ended, default to 0 until the first transaction commit.
        /// </summary>
        int TransactionSerialNumber { get; }

        /// <summary>
        /// Gets the last commit time. Defaults to <see cref="DateTime.UtcNow"/> at the very beginning,
        /// when no transaction has been comitted yet (and <see cref="TransactionSerialNumber"/> is 0).
        /// </summary>
        DateTime TransactionCommitTimeUtc { get; }

        /// <summary>
        /// Gets the secret key for this domain. It is a <see cref="System.Security.Cryptography.Rfc2898DeriveBytes"/> bytes
        /// array of length <see cref="ObservableDomain.DomainSecretKeyLength"/> derived from <see cref="Guid.NewGuid()"/>.
        /// </summary>
        ReadOnlySpan<byte> SecretKey { get; }
    }
}
