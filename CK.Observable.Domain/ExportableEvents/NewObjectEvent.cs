using System;

namespace CK.Observable
{
    public class NewObjectEvent : ObservableEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public ObjectExportedKind ExportedKind { get;}

        public NewObjectEvent( ObservableObject o, int oid )
            : base( ObservableEventType.NewObject )
        {
            ObjectId = oid;
            Object = o;
            ExportedKind = o.ExportedKind;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
            switch( ExportedKind )
            {
                case ObjectExportedKind.Object: e.Target.EmitString( "" ); break;
                case ObjectExportedKind.List: e.Target.EmitString( "A" ); break;
                case ObjectExportedKind.Map: e.Target.EmitString( "M" ); break;
                case ObjectExportedKind.Set: e.Target.EmitString( "S" ); break;
                default: throw new NotSupportedException();
            }
        }

        public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().Name}).";
    }
}
