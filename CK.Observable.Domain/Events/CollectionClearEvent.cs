using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class CollectionClearEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.CollectionClear;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public CollectionClearEvent( ObservableObject o )
        {
            ObjectId = o.OId;
            Object = o;
        }

        public override string ToString() => $"{EventType} {ObjectId}.";
    }
}
