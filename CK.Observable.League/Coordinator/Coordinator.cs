using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// The coordinator is the root object that exposes the current <see cref="Domains"/> of
    /// a <see cref="ObservableLeague"/> and allows to manage them.
    /// </summary>
    [SerializationVersion( 0 )]
    public class Coordinator : ObservableRootObject
    {
        readonly ObservableDictionary<string, Domain> _domains;
        IManagedLeague? _league;

        public Coordinator()
        {
            _domains = new ObservableDictionary<string, Domain>();
        }

        private protected Coordinator( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            _domains = (ObservableDictionary<string, Domain>)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( _domains );
        }

        /// <summary>
        /// Gets the map of the existing <see cref="Domain"/>.
        /// </summary>
        public IObservableReadOnlyDictionary<string, Domain> Domains => _domains;

        /// <summary>
        /// Attemps to create a new domain.
        /// Tests whether the domain type can be resolved: the root types must be available (this triggers any required assembly load).
        /// If the domain type cannot be resolved, this method returns null.
        /// </summary>
        /// <param name="name">The new domain name.</param>
        /// <param name="rootTypes">The root types.</param>
        /// <returns>The new domain or null on error.</returns>
        public Domain CreateDomain( string domainName, IEnumerable<string>? rootTypes )
        {
            if( String.IsNullOrWhiteSpace( domainName ) ) throw new ArgumentOutOfRangeException( nameof( domainName ) );
            Debug.Assert( _league != null );
            var roots = rootTypes?.ToArray() ?? Array.Empty<string>();
            IManagedDomain shell = _league!.CreateDomain( Domain.Monitor, domainName, roots );
            var d = new Domain( this, shell, roots );
            _domains.Add( domainName, d );
            return d;
        }

        /// <summary>
        /// Attemps to create a new domain.
        /// Tests whether the domain type can be resolved: the root types must be available (this triggers any required assembly load).
        /// If the domain type cannot be resolved, this method returns null.
        /// </summary>
        /// <param name="name">The new domain name.</param>
        /// <param name="rootTypes">The root types.</param>
        /// <returns>The new domain or null on error.</returns>
        public Domain CreateDomain( string domainName, params string[] rootTypes ) => CreateDomain( domainName, (IEnumerable<string>)rootTypes );


        internal void OnDisposeDomain( Domain domain )
        {
            _domains.Remove( domain.DomainName );
        }

        /// <summary>
        /// Gets the league. See <see cref="CoordinatorClient.League"/>.
        /// </summary>
        internal IManagedLeague League => _league!;

        internal void FinalizeConstruct( IManagedLeague league ) => _league = league;

        internal void Initialize( IActivityMonitor monitor, IManagedLeague league )
        {
            Debug.Assert( _domains.Values.All( d => d.Shell == null ) );
            _league = league;
            List<Domain> failed = null;
            foreach( var d in _domains.Values )
            {
                try
                {
                    d.Initialize( league.RebindDomain( monitor, d.DomainName, d.RootTypes ) );
                }
                catch( Exception ex )
                {
                    monitor.Error( $"Unable to bind to domain '{d.DomainName}'.", ex );
                    if( failed == null ) failed = new List<Domain>();
                    failed.Add( d );
                }
            }
            if( failed.Count > 0 )
            {
                monitor.Warn( $"Domains '{failed.Select( d => d.DomainName ).Concatenate("', '")}' must be removed." );
                foreach( var d in failed ) d.Dispose();
            }
        }


    }
}
