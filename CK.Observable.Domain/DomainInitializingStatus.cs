namespace CK.Observable
{
    /// <summary>
    /// Synthetic description of the current state of a domain.
    /// </summary>
    public enum DomainInitializingStatus
    {
        /// <summary>
        /// The domain is executing a regular transaction.
        /// </summary>
        None,

        /// <summary>
        /// The domain is being instantiated (the domain's constructor is currently executing).
        /// </summary>
        Instantiating,

        /// <summary>
        /// An error occurred during the last transaction and the domain is being
        /// rolled back by deserializing its last snapshot. 
        /// </summary>
        Rollingback,

        /// <summary>
        /// An error occurred during the last transaction and the domain is being
        /// rolled back by deserializing an old snapshot.
        /// <para>
        /// Once deserialized, some data may be outdated since the snapshot was not the
        /// reflect of the domain before the failing transaction started.
        /// </para>
        /// </summary>
        UnsafeRollingback,

        /// <summary>
        /// The domain is being reloaded from a persistent state.
        /// </summary>
        Deserializing
    }
}
