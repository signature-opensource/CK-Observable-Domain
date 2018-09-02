using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{

    public class NewPropertyEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.NewProperty;

        public int PropertyId { get; }

        public string Name { get; }

        public NewPropertyEvent( int id, string name )
        {
            PropertyId = id;
            Name = name;
        }

        public override string ToString() => $"{EventType} {Name} -> {PropertyId}.";

    }

}
