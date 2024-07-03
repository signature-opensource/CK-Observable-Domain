using CK.Cris;
using CK.StObj.TypeScript;

namespace CK.Observable.ServerSample.App
{
    public interface ISliderCommand : ICommand
    {
        public float SliderValue { get; set; }
    }
}
