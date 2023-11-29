using CK.Core;
using CK.Cris;
using CK.Observable.League;
using CK.Observable.MQTTWatcher;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample.App
{

    public class SliderCommandHandler : IAutoService
    {
        readonly DefaultObservableLeague _league;

        public SliderCommandHandler(DefaultObservableLeague league)
        {
            _league = league;
        }

        [CommandHandler]
        public async Task HandleSliderCommandAsync( IActivityMonitor m, ISliderCommand command )
        {
           await using var shell = await _league.GetShellAsync( m );

           // Run an empty transaction to ensure it doesn't send events
           await shell.ModifyThrowAsync(m, ( monitor, domain ) =>
           {
           } );

           await shell.ModifyThrowAsync(m, ( monitor, domain ) =>
           {
               domain.Root.Slider = command.SliderValue;
           } );
        }
    }


    public interface ISliderCommand : ICommand
    {
        public float SliderValue { get; set; }
    }
}
