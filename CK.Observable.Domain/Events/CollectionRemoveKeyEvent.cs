using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class CollectionRemoveKeyEvent : ObservableEvent, ICollectionEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public object Key { get; }

        public CollectionRemoveKeyEvent( ObservableObject o, object key )
            : base( ObservableEventType.CollectionRemoveKey )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
            ExportEventObject( e, Key );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Key}]";
    }
}
