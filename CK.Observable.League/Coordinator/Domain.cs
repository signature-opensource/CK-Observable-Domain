using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Describes a domain available in the <see cref="ObservableLeague"/>.
    /// </summary>
    [SerializationVersion(1)]
    public sealed class Domain : ObservableObject
    {
        IManagedDomain? _shell;
        string? _displayName;

        internal Domain( Coordinator coordinator, IManagedDomain shell, string[] rootTypes, ManagedDomainOptions? initialOptions )
        {
            Coordinator = coordinator;
            DomainName = shell.DomainName;
            RootTypes = rootTypes;
            // This enables the Shell/Client to centralize the default values of the Options.
            Options = initialOptions ?? shell.Options;
            _shell = shell;
            NextActiveTime = Util.UtcMinValue;
        }

        Domain( IBinaryDeserializer r, TypeReadInfo? info )
            : base( RevertSerialization.Default )
        {
            Coordinator = (Coordinator)r.ReadObject();
            DomainName = r.ReadString();
            _displayName = r.ReadNullableString();
            RootTypes = (string[])r.ReadObject();
            Options = (ManagedDomainOptions)r.ReadObject();
            if( info.Version > 0 )
            {
                NextActiveTime = r.ReadDateTime();
            }
            else
            {
                NextActiveTime = r.ReadBoolean() ? DateTime.UtcNow : Util.UtcMinValue;
            }
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Coordinator );
            w.Write( DomainName );
            w.WriteNullableString( _displayName );
            w.WriteObject( (string[])RootTypes );
            w.WriteObject( Options );
            w.Write( NextActiveTime );
        }

        internal IManagedDomain Shell => _shell!;

        /// <summary>
        /// This is called when the League is initially loaded
        /// or reloaded from its snapshot.
        /// </summary>
        /// <param name="shell">The associated shell.</param>
        internal void Initialize( IManagedDomain shell )
        {
            _shell = shell;
        }

        /// <summary>
        /// Gets the coordinator.
        /// </summary>
        public Coordinator Coordinator { get; }

        /// <summary>
        /// Gets whether the domain type can be resolved.
        /// </summary>
        public bool IsLoadable => Shell.IsLoadable;

        /// <summary>
        /// Gets whether this domain is currently loaded.
        /// </summary>
        public bool IsLoaded { get; internal set; }

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// Gets or sets a display name for this domain.
        /// Never null or empty: defaults to <see cref="DomainName"/>.
        /// </summary>
        public string DomainDisplayName
        {
            get => _displayName ?? DomainName;
            set =>  _displayName = String.IsNullOrWhiteSpace( value ) ? null : value;
        }

        /// <summary>
        /// Gets or sets the managed domain options.
        /// </summary>
        public ManagedDomainOptions Options { get; set; }

        /// <summary>
        /// Internal information that is managed by the Shell.
        /// This supports the <see cref="ManagedDomainOptions.LifeCycleOption"/> when <see cref="DomainLifeCycleOption.Default"/>.
        /// This is serialized so that when reloading a League, we know that the actual ObservableDomain
        /// must be preloaded. When the ObservableDomain is loaded, this is updated by
        /// <see cref="ObservableLeague.DomainClient.OnTransactionCommit(in SuccessfulTransactionEventArgs)"/>.
        /// </summary>
        internal DateTime NextActiveTime;

        /// <summary>
        /// Gets the types' assembly qualified names of the root objects (if any).
        /// There can be up to 4 typed roots. This list is empty if this is an untyped domain.
        /// </summary>
        public IReadOnlyList<string> RootTypes { get; }

        /// <inheritdoc />
        protected override void Dispose( bool shouldCleanup )
        {
            base.Dispose( shouldCleanup );
            if( shouldCleanup )
            {
                if( Shell != null )
                {
                    Shell.Destroy( Domain.Monitor, Coordinator.League );
                }
                Coordinator.OnDisposeDomain( this );
            }
        }


    }
}
