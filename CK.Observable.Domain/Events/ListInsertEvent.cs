using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ListInsertEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.ListInsert;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public object Item { get; }

        public ListInsertEvent( ObservableObject o, int index, object item )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
            Item = item;
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}] = {Item}.";

    }


}
