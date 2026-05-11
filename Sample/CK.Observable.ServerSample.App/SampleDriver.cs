using System.Threading.Tasks;
using CK.Core;
using CK.Cris;

namespace CK.Observable.ServerSample.App;

// Note: this attribute should be removed once the collection injection defines a dependency on the base class.
[RealObject( RequiredBy = [ typeof( ObservableDomainDriverHost ) ])]
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

    [CommandHandler]
    public async Task HandleMultiEventCommandAsync( IActivityMonitor monitor, IMultiEventCommand command )
    {
        await ModifyThrowAsync( monitor, ( _, _ ) =>
        {
            _singleton.Slider = command.SliderValue;
            _singleton.AddItem( command.Label, command.Counter );
            _singleton.AddItem( command.Label + "-2", command.Counter + 1 );
        } );
    }
}
