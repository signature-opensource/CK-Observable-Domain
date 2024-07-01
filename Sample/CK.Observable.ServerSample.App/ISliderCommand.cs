using CK.Cris;
using CK.StObj.TypeScript;

namespace CK.Observable.ServerSample.App
{
    [TypeScript]
    public interface ISliderCommand : ICommand
    {
        public float SliderValue { get; set; }
    }
}
