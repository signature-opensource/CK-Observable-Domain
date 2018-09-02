using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ListRemoveAtEvent : IObservableEvent
    {
        public ObservableEventType EventType => ObservableEventType.ListRemoveAt;

        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public ListRemoveAtEvent( ObservableObject o, int index )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}].";

    }


}
