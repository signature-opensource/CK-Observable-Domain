using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class CollectionRemoveKeyEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.CollectionRemoveKey;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public object Key { get; }

        public CollectionRemoveKeyEvent( ObservableObject o, object key )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Key}]";
    }
}
