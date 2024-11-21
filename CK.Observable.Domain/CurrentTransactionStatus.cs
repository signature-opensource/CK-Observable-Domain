using System;

namespace CK.Observable;

/// <summary>
/// Synthetic description of the current state of a domain.
/// </summary>
public enum CurrentTransactionStatus
{
    /// <summary>
    /// The domain is executing a regular transaction.
    /// </summary>
    Regular = 0,

    /// <summary>
    /// The domain is being reloaded from a persistent state.
    /// </summary>
    Deserializing = 1,

    /// <summary>
    /// An error occurred during the current transaction and the domain is being
    /// rolled back by deserializing its last snapshot. 
    /// </summary>
    Rollingback = 2,

    /// <summary>
    /// An error occurred during the last transaction and the domain is being
    /// rolled back by deserializing an old snapshot.
    /// <para>
    /// Once deserialized, some data may be outdated since the snapshot was not the
    /// reflect of the domain before the failing transaction started.
    /// </para>
    /// </summary>
    DangerousRollingback = 3,

    /// <summary>
    /// The domain is being instantiated (the domain's constructor is currently executing).
    /// </summary>
    Instantiating = 4,

    /// <summary>
    /// The domain is being disposed.
    /// </summary>
    Disposing = 8
}

/// <summary>
/// Provides extensions to <see cref="CurrentTransactionStatus"/>.
/// </summary>
public static class CurrentTransactionStatusExtensions
{
    /// <summary>
    /// Gets whether this status is <see cref="CurrentTransactionStatus.Deserializing"/>, <see cref="CurrentTransactionStatus.Rollingback"/>
    /// or <see cref="CurrentTransactionStatus.DangerousRollingback"/>.
    /// </summary>
    /// <param name="s">This status.</param>
    /// <returns>True if the domain is being deserialized, false otherwise.</returns>
    public static bool IsDeserializing( this CurrentTransactionStatus s ) => ((int)s & 3) != 0;

    /// <summary>
    /// Gets whether this status is <see cref="CurrentTransactionStatus.Regular"/>.
    /// </summary>
    /// <param name="s">This status.</param>
    /// <returns>True if the domain is inside a regular transaction, false otherwise.</returns>
    public static bool IsRegular( this CurrentTransactionStatus s ) => s == CurrentTransactionStatus.Regular;
}
