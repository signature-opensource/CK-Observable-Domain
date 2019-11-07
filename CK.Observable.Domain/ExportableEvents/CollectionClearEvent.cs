namespace CK.Observable
{
    public class CollectionClearEvent : ObservableEvent, ICollectionEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public CollectionClearEvent( ObservableObject o )
            : base( ObservableEventType.CollectionClear )
        {
            ObjectId = o.OId;
            Object = o;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
        }

        public override string ToString() => $"{EventType} {ObjectId}.";
    }
}
