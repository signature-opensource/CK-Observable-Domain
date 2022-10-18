using CK.Core;
using CK.Cris;
using CK.Observable.League;
using CK.Observable.MQTTWatcher;

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
        public async Task HandleSliderCommand( IActivityMonitor m, ISliderCommand command )
        {
           await using var shell = await _league.GetShellAsync( m );
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
