using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class CollectionMapSetEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.CollectionRemoveKey;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public object Key { get; }

        public object Value { get; }

        public CollectionMapSetEvent( ObservableObject o, object key, object value )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
            Value = value;
        }


        public override string ToString() => $"{EventType} {ObjectId}[{Key}] = {Value ?? "null"}";
    }
}
