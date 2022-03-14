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
    public sealed class Coordinator : ObservableRootObject
    {
        readonly ObservableDictionary<string, Domain> _domains;
        IManagedLeague? _league;

        /// <summary>
        /// Initializes a new <see cref="Coordinator"/>.
        /// </summary>
        public Coordinator()
        {
            _domains = new ObservableDictionary<string, Domain>();
        }

        Coordinator( IBinaryDeserializer r, TypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
        {
            _domains = (ObservableDictionary<string, Domain>)r.ReadObject();
        }

        Coordinator( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            _domains = r.ReadObject<ObservableDictionary<string, Domain>>();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in Coordinator o )
        {
            w.WriteObject( o._domains );
        }

        /// <summary>
        /// Gets the map of the existing <see cref="Domain"/>.
        /// </summary>
        public IObservableReadOnlyDictionary<string, Domain> Domains => _domains;

        /// <summary>
        /// Creates a new domain with a unique name.
        /// <para>
        /// This throws an <see cref="InvalidOperationException"/> if the domain name already exists,
        /// and can throw any exception if root type names cannot be resolved or instantiation fails.
        /// </para>
        /// </summary>
        /// <param name="domainName">The new domain name.</param>
        /// <param name="rootTypes">The root types (can be empty: a basic <see cref="ObservableDomain"/> is created).</param>
        /// <param name="initialOptions">The optional initial <see cref="Domain.Options"/>.</param>
        /// <returns>The new domain.</returns>
        public Domain CreateDomain(string domainName, IEnumerable<string>? rootTypes, ManagedDomainOptions? initialOptions = null)
        {
            if( String.IsNullOrWhiteSpace( domainName ) ) throw new ArgumentOutOfRangeException( nameof( domainName ) );
            Debug.Assert( _league != null );
            var roots = rootTypes?.ToArray() ?? Array.Empty<string>();
            IManagedDomain shell = _league.CreateDomain( Domain.Monitor, domainName, roots );
            // Default options are provided by the shell: their default values are provided by the
            // Client and JsonEventCollector code.
            var d = new Domain( this, shell, roots, initialOptions ); 
            _domains.Add( domainName, d );
            return d;
        }

        /// <summary>
        /// Creates a new domain with a unique name.
        /// <para>
        /// This throws an <see cref="InvalidOperationException"/> if the domain name already exists,
        /// and can throw any exception if root type names cannot be resolved or instantiation fails.
        /// </para>
        /// </summary>
        /// <param name="domainName">The new domain name.</param>
        /// <param name="rootTypes">The root types (can be empty: a basic <see cref="ObservableDomain"/> is created).</param>
        /// <returns>The new domain.</returns>
        public Domain CreateDomain( string domainName, params string[] rootTypes ) => CreateDomain(domainName, (IEnumerable<string>)rootTypes);


        internal void OnDestroyDomain( Domain domain )
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
            List<Domain>? failed = null;
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
            if( failed != null )
            {
                monitor.Warn( $"Domains '{failed.Select( d => d.DomainName ).Concatenate("', '")}' must be removed." );
                foreach( var d in failed ) d.Destroy();
            }
        }


    }
}
