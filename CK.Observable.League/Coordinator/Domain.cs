using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Describes a domain available in the <see cref="ObservableLeague"/>.
    /// </summary>
    [SerializationVersion(0)]
    public sealed class Domain : ObservableObject
    {
        internal Domain( Coordinator coordinator, IManagedDomain shell, string[] rootTypes )
        {
            Coordinator = coordinator;
            DomainName = shell.DomainName;
            RootTypes = rootTypes;
            Shell = shell;
        }

        Domain( IBinaryDeserializerContext c )
        {
            var r = c.StartReading();
            Coordinator = (Coordinator)r.ReadObject();
            DomainName = r.ReadString();
            RootTypes = (string[])r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Coordinator );
            w.Write( DomainName );
            w.WriteObject( (string[])RootTypes );
        }

        internal IManagedDomain? Shell { get; set; }

        /// <summary>
        /// Gets the coordinator.
        /// </summary>
        public Coordinator Coordinator { get; }

        /// <summary>
        /// Gets whether the domain type can be resolved.
        /// </summary>
        public bool IsLoadable => Shell!.IsLoadable;

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// Gets or sets the managed domain options.
        /// </summary>
        public ManagedDomainOptions Options { get; set; }

        /// <summary>
        /// Gets the types' assembly qualified names of the root objects (if any).
        /// There can be up to 4 typed roots. This list is empty if this is an untyped domain.
        /// </summary>
        public IReadOnlyList<string> RootTypes { get; }

        void OnOptionsChanged( object before, object after )
        {
            if( IsDeserializing ) return;
            Shell!.Options = Options;
        }

        protected override void Dispose( bool shouldCleanup )
        {
            base.Dispose( shouldCleanup );
            if( shouldCleanup ) Coordinator.RemoveDomain( this );
        }

    }
}
