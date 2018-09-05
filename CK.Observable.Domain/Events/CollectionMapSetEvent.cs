using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class CollectionMapSetEvent : ObservableEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public object Key { get; }

        public object Value { get; }

        public CollectionMapSetEvent( ObservableObject o, object key, object value )
            : base( ObservableEventType.CollectionRemoveKey )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
            Value = value;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
            ExportEventObject( e, Key );
            ExportEventObject( e, Value );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Key}] = {Value ?? "null"}";
    }
}
