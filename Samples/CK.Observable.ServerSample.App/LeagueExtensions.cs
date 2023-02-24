using CK.Core;
using CK.Observable.League;
using System;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample.App;

public static class LeagueExtensions
{
    public static async Task<IObservableDomainShell<Root>> GetShellAsync( this DefaultObservableLeague defaultObservableLeague, IActivityMonitor m )
    {
        var loader = defaultObservableLeague[ "Test-Domain" ];
        if( loader == null ) throw new InvalidOperationException( $"Domain Test-Domain is not loaded." );
        var shell = await loader.LoadAsync<Root>( m );
        if( shell == null ) throw new InvalidOperationException( $"Domain Test-Domain could not be loaded." );
        return shell;
    }
}
