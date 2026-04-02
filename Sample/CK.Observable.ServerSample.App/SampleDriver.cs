using System.Threading.Tasks;
using CK.Core;
using CK.Cris;

namespace CK.Observable.ServerSample.App;

public sealed class SampleDriver : TransientDomainDriver<SampleDriver>
{
    SampleSingleton _singleton = null!;

    public SampleDriver()
        : base( "Test-Domain", autoStartTimer: false )
    {
    }

    protected override void PreConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
        _singleton = domain.CreateSingleton<SampleSingleton>();
    }

    [CommandHandler]
    public async Task HandleSliderCommandAsync( IActivityMonitor monitor, ISliderCommand command )
    {
        await ModifyThrowAsync( monitor, ( _, _ ) =>
        {
            _singleton.Slider = command.SliderValue;
        } );
    }
}
