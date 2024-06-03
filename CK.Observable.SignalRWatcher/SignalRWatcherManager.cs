using System.Collections.Generic;
using System.Threading.Tasks;
using CK.Core;
using CK.Cris;

namespace CK.Observable.SignalRWatcher
{
    public sealed class SignalRWatcherManager : ISingletonAutoService
    {
        readonly Dictionary<string, HubObservableWatcher> _watchers = new();

        [CommandHandler]
        public async Task<string> HandleStartOrRestartWatchAsync( IActivityMonitor monitor,
                                                                  ISignalRObservableWatcherStartOrRestartCommand command )
        {
            HubObservableWatcher? val;
            lock( _watchers )
            {
                if( !_watchers.TryGetValue( command.ClientId, out val ) )
                {
                    Throw.InvalidDataException( $"{command.ClientId} does not exists, or is not identified as you." );
                }
            }

            var watchEvent =
                await val.GetStartOrRestartEventAsync( monitor, command.DomainName, command.TransactionNumber );
            return watchEvent.JsonExport;
        }

        internal void AddWatcher( string key, HubObservableWatcher watcher )
        {
            lock( _watchers )
            {
                _watchers.Add( key, watcher );
            }
        }

        internal void ReleaseWatcher( string key )
        {
            lock( _watchers )
            {
                _watchers.Remove( key, out var val );
                val?.Dispose();
            }
        }
    }
}
