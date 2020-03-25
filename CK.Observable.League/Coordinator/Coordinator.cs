using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Observable.League
{
    [SerializationVersion( 0 )]
    public class Coordinator : ObservableRootObject
    {
        readonly ObservableDictionary<string, Domain> _domains;

        internal Coordinator( ObservableDomain domain )
            : base( domain )
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

        internal ObservableLeague? ObservableLeague { get; set; }

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
            Debug.Assert( ObservableLeague != null );
            var roots = rootTypes?.ToArray() ?? Array.Empty<string>();
            IManagedDomain shell = ObservableLeague!.CreateDomain( Monitor, domainName, roots );
            var d = new Domain( this, shell, roots );
            _domains.Add( domainName, d );
            return d;
        }

        internal void RemoveDomain( Domain domain ) => _domains.Remove( domain.DomainName );

        /// <summary>
        /// Gets the map of the <see cref="Domain"/>.
        /// </summary>
        public IObservableReadOnlyDictionary<string, Domain> Domains => _domains;


    }
}
