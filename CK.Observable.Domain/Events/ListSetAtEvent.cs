using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ListSetAtEvent : ObservableEvent, ICollectionEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public object Value { get; }


        public ListSetAtEvent( ObservableObject o, int index, object value )
            : base( ObservableEventType.ListSetAt )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
            Value = value;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
            e.Target.EmitInt32( Index );
            ExportEventObject( e, Value );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}] = {Value ?? "null"}.";

    }


}
