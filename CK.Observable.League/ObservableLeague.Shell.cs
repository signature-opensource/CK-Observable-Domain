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
            readonly SemaphoreSlim? _loadLock;
            readonly IActivityMonitor _initialMonitor;
            Type? _domainType;
            int _refCount;
            ObservableDomain? _domain;

            class IndependentShell : IObservableDomainShell
            {
                readonly IObservableDomainShell _shell;
                readonly IActivityMonitor _monitor;

                public IndependentShell( Shell s, IActivityMonitor m )
                {
                    _shell = s;
                    _monitor = m;
                }

                string IObservableDomainShell.DomainName => _shell.DomainName;

                bool IObservableDomainShell.IsDestroyed => _shell.IsDestroyed;

                ValueTask IObservableDomainShell.DisposeAsync( IActivityMonitor monitor ) => _shell.DisposeAsync( monitor );
                
                Task<TransactionResult> IObservableDomainShell.ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain>? actions, int millisecondsTimeout )
                {
                    return _shell.ModifyAsync( monitor, actions, millisecondsTimeout );
                }

                void IObservableDomainShell.Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader, int millisecondsTimeout )
                {
                    _shell.Read( monitor, reader, millisecondsTimeout );
                }

                T IObservableDomainShell.Read<T>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain, T> reader, int millisecondsTimeout )
                {
                    return _shell.Read( monitor, reader, millisecondsTimeout );
                }

                Task<(TransactionResult, Exception)> IObservableDomainShell.SafeModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain>? actions, int millisecondsTimeout )
                {
                    return _shell.SafeModifyAsync( monitor, actions, millisecondsTimeout );
                }

                Task<bool> IObservableDomainShell.SaveAsync( IActivityMonitor monitor )
                {
                    return _shell.SaveAsync( monitor );
                }

                ValueTask IAsyncDisposable.DisposeAsync() => _shell.DisposeAsync( _monitor );
            }

            /// <summary>
            /// Attempts to synthesize the domain type (from the root types).
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="domainName">The name of the domain.</param>
            /// <param name="store">The persistent store.</param>
            /// <param name="rootTypes">The root types.</param>
            internal Shell( IActivityMonitor monitor, string domainName, IStreamStore store, IReadOnlyList<string> rootTypes )
            {
                _initialMonitor = monitor;
                _client = new StreamStoreClient( domainName, store );
                RootTypes = rootTypes;
                if( RootTypes.Count == 0 )
                {
                    _domainType = typeof( ObservableDomain );
                    _loadLock = new SemaphoreSlim( 1 );
                }
                else
                {
                    bool success = true;
                    Type[] types = new Type[RootTypes.Count];
                    for( int i = 0; i < RootTypes.Count; ++i )
                    {
                        if( (types[i] = SimpleTypeFinder.WeakResolver( RootTypes[i], false )) == null )
                        {
                            monitor.Error( $"Unable to resolve root type '{RootTypes[i]}' for domain '{DomainName}'." );
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

            public bool IsDestroyed { get; private set; }

            public IReadOnlyList<string> RootTypes { get; }

            public bool IsLoadable => _domainType != null;

            public bool IsLoaded => _refCount != 0;

            internal bool ClosingLeague { get; set; }

            public ManagedDomainOptions Options
            {
                get => new ManagedDomainOptions(
                            c: _client.CompressionKind,
                            snapshotSaveDelay: TimeSpan.FromMilliseconds( _client.SnapshotSaveDelay ),
                            snapshotKeepDuration: _client.SnapshotKeepDuration,
                            snapshotMaximalTotalKiB: _client.SnapshotMaximalTotalKiB,
                            eventKeepDuration: _client.TransactClient.KeepDuration,
                            eventKeepLimit: _client.TransactClient.KeepLimit );
                set
                {
                    _client.CompressionKind = value.CompressionKind;
                    _client.SnapshotSaveDelay = (int)value.SnapshotSaveDelay.TotalMilliseconds;
                    _client.SnapshotKeepDuration = value.SnapshotKeepDuration;
                    _client.SnapshotMaximalTotalKiB = value.SnapshotMaximalTotalKiB;
                    _client.TransactClient.KeepDuration = value.ExportedEventKeepDuration;
                    _client.TransactClient.KeepLimit = value.ExportedEventKeepLimit;
                }
            }

            void IManagedDomain.Destroy( IActivityMonitor monitor, IManagedLeague league )
            {
                IsDestroyed = true;
                league.OnDestroy( monitor, this );
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
                if( !IsLoadable || IsDestroyed || ClosingLeague ) return null;
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
                if( _domain == null ) return null;
                if( _initialMonitor == monitor ) return this;
                return new IndependentShell( this, monitor );
            }

            ValueTask IObservableDomainShell.DisposeAsync( IActivityMonitor monitor ) => DoShellDisposeAsync( monitor );

            ValueTask IAsyncDisposable.DisposeAsync() => DoShellDisposeAsync( _initialMonitor );

            async ValueTask DoShellDisposeAsync( IActivityMonitor monitor )
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
                            await( IsDestroyed ? _client.ArchiveAsync( monitor ) : _client.SaveAsync( monitor ) );
                            _domain.Dispose( monitor );
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
