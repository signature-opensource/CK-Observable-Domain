using CK.Cris;

namespace CK.Observable.ServerSample.App;

public interface ISliderCommand : ICommand
{
    public float SliderValue { get; set; }
}
