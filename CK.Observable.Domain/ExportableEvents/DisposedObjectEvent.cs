namespace CK.Observable
{
    public class DisposedObjectEvent : ObservableEvent
    {
        public ObservableObject.Id ObjectId { get; }

        public ObservableObject Object { get; }

        public DisposedObjectEvent( ObservableObject o )
            : base( ObservableEventType.DisposedObject )
        {
            ObjectId = o.ObjectId;
            Object = o;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
        }

        public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().Name}).";

    }
}
