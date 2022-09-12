using CK.BinarySerialization;
using CK.Core;
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
    public sealed class OCoordinatorRoot : ObservableRootObject
    {
        readonly ObservableDictionary<string, ODomain> _domains;
        IManagedLeague? _league;

        /// <summary>
        /// Initializes a new <see cref="OCoordinatorRoot"/>.
        /// </summary>
        public OCoordinatorRoot()
        {
            _domains = new ObservableDictionary<string, ODomain>();
        }

        OCoordinatorRoot( IBinaryDeserializer r, ITypeReadInfo info )
                : base( Sliced.Instance )
        {
            _domains = r.ReadObject<ObservableDictionary<string, ODomain>>();
        }

        public static void Write( IBinarySerializer w, in OCoordinatorRoot o )
        {
            w.WriteObject( o._domains );
        }

        /// <summary>
        /// Gets the map of the existing <see cref="ODomain"/>.
        /// </summary>
        public IObservableReadOnlyDictionary<string, ODomain> Domains => _domains;

        /// <summary>
        /// Creates a new domain with a unique name.
        /// <para>
        /// This throws an <see cref="InvalidOperationException"/> if the domain name already exists,
        /// and can throw any exception if root type names cannot be resolved or instantiation fails.
        /// </para>
        /// </summary>
        /// <param name="domainName">The new domain name.</param>
        /// <param name="rootTypes">The root types (can be empty: a basic <see cref="ObservableDomain"/> is created).</param>
        /// <param name="initialOptions">The optional initial <see cref="ODomain.Options"/>.</param>
        /// <returns>The new domain.</returns>
        public ODomain CreateDomain( string domainName, IEnumerable<string>? rootTypes, ManagedDomainOptions? initialOptions = null )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( domainName );
            Debug.Assert( _league != null );
            var roots = rootTypes?.ToArray() ?? Array.Empty<string>();
            IInternalManagedDomain shell = _league.CreateDomain( Domain.Monitor, domainName, roots );
            // Default options are provided by the shell: their default values are provided by the
            // Client and JsonEventCollector code.
            var d = new ODomain( this, shell, roots, initialOptions );
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
        public ODomain CreateDomain( string domainName, params string[] rootTypes ) => CreateDomain( domainName, (IEnumerable<string>)rootTypes );


        internal void OnDestroyDomain( ODomain domain )
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
            List<ODomain>? failed = null;
            foreach( var d in _domains.Values )
            {
                try
                {
                    d.Initialize( league.RebindDomain( monitor, d.DomainName, d.RootTypes ) );
                }
                catch( Exception ex )
                {
                    monitor.Error( $"Unable to bind to domain '{d.DomainName}'.", ex );
                    if( failed == null ) failed = new List<ODomain>();
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
