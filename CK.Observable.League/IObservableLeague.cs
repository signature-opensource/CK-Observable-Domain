namespace CK.Observable.League
{
    /// <summary>
    /// <para>
    /// Interface for a class that manages a bunch of <see cref="ObservableDomain"/>.
    /// </para>
    /// <para>
    /// To interact with existing domains, a <see cref="IObservableDomainLoader"/> must be obtained
    /// thanks to the <see cref="this[string]"/> accessor (or the more explicit <see cref="Find(string)"/> method).
    /// </para>
    /// <para>
    /// Creation and destruction of domains are under control of the <see cref="Coordinator"/> domain.
    /// </para>
    /// </summary>
    public interface IObservableLeague
    {
        /// <summary>
        /// Finds an existing domain.
        /// </summary>
        /// <param name="domainName">The domain name to find.</param>
        /// <returns>The managed domain or null if not found.</returns>
        public IObservableDomainLoader? Find( string domainName );

        /// <summary>
        /// Shortcut of <see cref="Find(string)"/>.
        /// </summary>
        /// <param name="domainName">The domain name to find.</param>
        /// <returns>The managed domain or null if not found.</returns>
        public IObservableDomainLoader? this[string domainName] { get; }

        /// <summary>
        /// Gets the access to the Coordinator domain.
        /// </summary>
        public IObservableDomainAccess<OCoordinatorRoot> Coordinator { get; }
    }
}
