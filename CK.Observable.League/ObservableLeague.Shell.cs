using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    public partial class ObservableLeague
    {
        partial class Shell : IObservableDomainLoader, IObservableDomainShell, IManagedDomain
        {
            readonly StreamStoreClient _client;
            readonly IReadOnlyList<string> _rootTypes;
            readonly SemaphoreSlim? _loadLock;
            Type? _domainType;
            int _refCount;
            ObservableDomain? _domain;

            /// <summary>
            /// Attempts to synthesize the domain type (from the root types).
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="domainName">The name of the domain.</param>
            /// <param name="store">The persistent store.</param>
            /// <param name="rootTypes">The root types.</param>
            internal Shell( IActivityMonitor monitor, string domainName, IStreamStore store, IReadOnlyList<string> rootTypes )
            {
                _client = new StreamStoreClient( domainName, store );
                _rootTypes = rootTypes;
                if( _rootTypes.Count == 0 ) _domainType = typeof( ObservableDomain );
                else
                {
                    bool success = true;
                    Type[] types = new Type[_rootTypes.Count];
                    for( int i = 0; i < _rootTypes.Count; ++i )
                    {
                        if( (types[i] = SimpleTypeFinder.WeakResolver( _rootTypes[i], false )) == null )
                        {
                            monitor.Error( $"Unable to resolve root type '{_rootTypes[i]}' for domain '{DomainName}'." );
                            success = false;
                        }
                    }
                    if( success )
                    {
                        _domainType = types.Length switch
                        {
                            1 => typeof( ObservableDomain<> ).MakeGenericType( types ),
                            2 => typeof( ObservableDomain<,> ).MakeGenericType( types ),
                            3 => typeof( ObservableDomain<,,> ).MakeGenericType( types ),
                            _ => typeof( ObservableDomain<,,,> ).MakeGenericType( types )
                        };
                        _loadLock = new SemaphoreSlim( 1 );
                    }
                }
            }

            public string DomainName => _client.DomainName;

            public bool IsLoadable => _domainType != null;

            public bool IsLoaded => _refCount != 0;

            public ManagedDomainOptions Options
            {
                get => new ManagedDomainOptions( _client.CompressionKind, _client.AutoSaveTime, _client.TransactClient.KeepDuration, _client.TransactClient.KeepLimit );
                set 
                {
                    _client.CompressionKind = value.CompressionKind;
                    _client.AutoSaveTime = value.AutoSaveTime;
                    _client.TransactClient.KeepDuration = value.ExportedEventKeepDuration;
                    _client.TransactClient.KeepLimit = value.ExportedEventKeepLimit;
                }
            }

            async Task<(TransactionResult, Exception)> IObservableDomainShell.SafeModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain>? actions, int millisecondsTimeout )
            {
                var d = _domain!;
                return await d.SafeModifyAsync( monitor, () => actions?.Invoke( monitor, d ), millisecondsTimeout );
            }

            Task<TransactionResult> IObservableDomainShell.ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain>? actions, int millisecondsTimeout )
            {
                var d = _domain!;
                return d.ModifyAsync( monitor, () => actions?.Invoke( monitor, d ), millisecondsTimeout );
            }

            void IObservableDomainShell.Read(IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader, int millisecondsTimeout)
            {
                var d = _domain!;
                using( d.AcquireReadLock( millisecondsTimeout ) )
                {
                    reader( monitor, d );
                }
            }

            T IObservableDomainShell.Read<T>(IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain, T> reader, int millisecondsTimeout)
            {
                var d = _domain!;
                using( d.AcquireReadLock( millisecondsTimeout ) )
                {
                    return reader( monitor, d );
                }
            }

            Task<bool> IObservableDomainShell.SaveAsync( IActivityMonitor m )
            {
                return _client.SaveAsync( m );
            }

            public async Task<IObservableDomainShell?> LoadAsync( IActivityMonitor monitor )
            {
                if( !IsLoadable ) return null;
                await _loadLock!.WaitAsync();
                if( ++_refCount == 1 )
                {
                    Debug.Assert( _domain == null );
                    try
                    {
                        var d = (ObservableDomain)Activator.CreateInstance( _domainType, monitor, DomainName, _client );
                        await _client.InitializeAsync( monitor, d );
                        _domain = d;
                    }
                    catch( Exception ex )
                    {
                        Interlocked.Decrement( ref _refCount );
                        monitor.Error( $"Unable to instanciate and load '{DomainName}'.", ex );
                        _refCount = 0;
                    }
                }
                _loadLock.Release();
                return _domain != null ? this : null;
            }

            async Task IObservableDomainShell.DisposeAsync( IActivityMonitor monitor )
            {
                if( !IsLoadable ) throw new ObjectDisposedException( nameof( IObservableDomainShell ) );
                await _loadLock!.WaitAsync();
                if( --_refCount < 0 ) throw new ObjectDisposedException( nameof( IObservableDomainShell ) );
                if( _refCount == 0 )
                {
                    try
                    {
                        if( _domain != null )
                        {
                            await _client.SaveAsync( monitor );
                            _domain.Dispose();
                        }
                    }
                    finally
                    {
                        _domain = null;
                    }
                }
                _loadLock.Release();
            }

        }

    }
}
