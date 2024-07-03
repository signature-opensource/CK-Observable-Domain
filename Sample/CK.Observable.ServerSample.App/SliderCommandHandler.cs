using System;
using CK.Core;
using CK.Cris;
using CK.Observable.League;
using CK.Observable.MQTTWatcher;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample.App
{

    public class SliderCommandHandler : IAutoService
    {
        static readonly Random Random = new Random();
        readonly DefaultObservableLeague _league;

        public SliderCommandHandler(DefaultObservableLeague league)
        {
            _league = league;
        }

        [CommandHandler]
        public async Task HandleSliderCommandAsync( IActivityMonitor m, ISliderCommand command )
        {
           await using var shell = await _league.GetShellAsync( m );

           // Run a bunch of empty transactions to ensure they don't send events to front-end
           for( int i = 0; i < Random.Next( 0, 10 ); i++ )
           {
               await shell.ModifyThrowAsync(m, ( monitor, domain ) =>
               {
               } );
           }

            var x1 = typeof( CK.Cris.AspNet.CrisAspNetService );
            var x2 = typeof( CK.Auth.CrisAuthenticationService );
            var x3 = typeof( CK.Observable.MQTTWatcher.DefaultMQTTObservableServer );
            var x4 = typeof( CK.Observable.SignalRWatcher.HubObservableWatcher );

           await shell.ModifyThrowAsync(m, ( monitor, domain ) =>
           {
               domain.Root.Slider = command.SliderValue;
           } );
        }
    }
}
