using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CK.Observable.League;

public partial class ObservableLeague
{
    /// <summary>
    /// Applies a set of <see cref="EnsureDomainOptions"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="ensureDomains">The set of domain to create if they don't exist already.</param>
    /// <returns>The awaitable.</returns>
    public async Task ApplyEnsureDomainOptionsAsync( IActivityMonitor monitor, IReadOnlyCollection<EnsureDomainOptions> ensureDomains )
    {
        var ensured = ProcessEnsureDomain( monitor, ensureDomains );
        if( ensured != null )
        {
            Debug.Assert( ensured.Count > 0 );
            using( monitor.OpenInfo( $"Applying {ensured.Count} valid DefaultDomainEnsureOptions." ) )
            {
                for( int i = 0; i < ensured.Count; ++i )
                {
                    var (types, option) = ensured[i];
                    Debug.Assert( !string.IsNullOrEmpty( option.DomainName ) );
                    var d = Find( option.DomainName );
                    if( d != null )
                    {
                        if( d.RootTypes.Count >= types.Length && types.SequenceEqual( d.RootTypes.Take( types.Length ) ) )
                        {
                            monitor.Trace( d.RootTypes.Count == types.Length
                                                ? $"Domain '{d.DomainName}' found. Root types '{GetTypeNames( types )}' match. Everything is fine."
                                                : $"Domain '{d.DomainName}' found. Expected root types '{GetTypeNames( types )}' are satisfied by '{GetTypeNames( d.RootTypes )}'. Everything is fine." );
                        }
                        else
                        {
                            monitor.Error( $"Domain '{d.DomainName}' found but expected root types '{GetTypeNames( types )}' don't match actual ones '{GetTypeNames( d.RootTypes )}'. Skipping configuration." );
                        }
                        ensured.RemoveAt( i-- );
                    }
                }
                if( ensured.Count > 0 )
                {
                    using( monitor.OpenInfo( $"Creating {ensured.Count} domains from DefaultDomainEnsureOptions." ) )
                    {
                        await Coordinator.ModifyThrowAsync( monitor, ( monitor, d ) =>
                        {
                            foreach( var e in ensured )
                            {
                                var o = e.Options;
                                var initialOptions = new ManagedDomainOptions( o.CreateLifeCycleOption,
                                                                               o.CreateCompressionKind,
                                                                               o.CreateSkipTransactionCount,
                                                                               o.CreateSnapshotSaveDelay,
                                                                               o.CreateSnapshotKeepDuration,
                                                                               o.CreateSnapshotMaximalTotalKiB,
                                                                               o.CreateExportedEventKeepDuration,
                                                                               o.CreateExportedEventKeepLimit,
                                                                               o.CreateHousekeepingRate,
                                                                               o.CreateDebugMode );
                                d.Root.CreateDomain( o.DomainName!, o.RootTypes, initialOptions );
                            }
                        }, parallelDomainPostActions: false, waitForDomainPostActionsCompletion: true );
                    }
                }
            }
        }

        static List<(Type[] RootTypes, EnsureDomainOptions Options)>? ProcessEnsureDomain( IActivityMonitor monitor,
                                                                                           IReadOnlyCollection<EnsureDomainOptions> ensureDomains )
        {
            if( ensureDomains.Count == 0 ) return null;

            List<(Type[] RootTypes, EnsureDomainOptions Options)>? result = null;
            foreach( var e in ensureDomains )
            {
                bool success = true;
                if( string.IsNullOrEmpty( e.DomainName ) )
                {
                    success = false;
                    monitor.Error( $"Invalid DomainName in EnsureDomainOptions: it cannot be empty." );
                }
                var types = e.RootTypes.Count > 0 ? new Type[e.RootTypes.Count] : Type.EmptyTypes;
                for( int i = 0; i < e.RootTypes.Count; ++i )
                {
                    var tName = e.RootTypes[i];
                    var t = SimpleTypeFinder.WeakResolver( tName, throwOnError: false );
                    if( t == null )
                    {
                        success = false;
                        monitor.Error( $"Unable to load domain root type '{tName}' for domain '{e.DomainName}'. The ensured domain is ignored." );
                    }
                    else types[i] = t;
                }
                if( success )
                {
                    if( result == null ) result = new List<(Type[] RootTypes, EnsureDomainOptions Options)>();
                    else
                    {
                        if( result.IndexOf( c => c.Options.DomainName == e.DomainName ) >= 0 )
                        {
                            success = false;
                            monitor.Error( $"Duplicate Domain name '{e.DomainName}': a EnsureDomainOptions with this name is already specified, this one is ignored." );
                        }
                    }
                    if( success )
                    {
                        result.Add( (types, e) );
                    }
                }
            }
            return result;
        }

        static string GetTypeNames( IEnumerable<Type> types )
        {
            return types.Select( t => t.ToCSharpName() ).Concatenate( "', '" );
        }
    }

}
