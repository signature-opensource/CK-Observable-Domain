namespace CK.Observable
{
    /// <summary>
    /// Captures the transaction that has been rolled back.
    /// </summary>
    public sealed class RolledbackTransactionInfo
    {
        /// <summary>
        /// Gets the failed transaction.
        /// </summary>
        public TransactionResult Failure { get; }

        /// <summary>
        /// Gets whether the domain has been restored from its last snapshot.
        /// </summary>
        public bool IsSafeRollback { get; }

        /// <summary>
        /// Gets whether the domain has been restored from an old snapshot.
        /// </summary>
        public bool IsDangerousRollback => !IsSafeRollback;

        internal RolledbackTransactionInfo( TransactionResult r, bool safeRollback )
        {
            Failure = r;
            IsSafeRollback = safeRollback;
        }
    }
}
