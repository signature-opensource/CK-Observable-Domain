using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class NewObjectEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.NewObject;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public NewObjectEvent( ObservableObject o, int oid )
        {
            ObjectId = oid;
            Object = o;
        }

        public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().Name}).";
    }
}
