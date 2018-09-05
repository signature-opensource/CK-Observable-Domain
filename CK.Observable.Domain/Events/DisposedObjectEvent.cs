using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class DisposedObjectEvent : ObservableEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public DisposedObjectEvent( ObservableObject o )
            : base( ObservableEventType.DisposedObject )
        {
            ObjectId = o.OId;
            Object = o;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
        }

        public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().Name}).";

    }
}
