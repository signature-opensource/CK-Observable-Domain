using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class DisposedObjectEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.DisposedObject;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public DisposedObjectEvent( ObservableObject o )
        {
            ObjectId = o.OId;
            Object = o;
        }

        public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().Name}).";

    }
}
