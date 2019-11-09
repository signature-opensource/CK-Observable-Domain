namespace CK.Observable
{
    public class CollectionClearEvent : ObservableEvent, ICollectionEvent
    {
        public ObservableObjectId ObjectId { get; }

        public ObservableObject Object { get; }

        public CollectionClearEvent( ObservableObject o )
            : base( ObservableEventType.CollectionClear )
        {
            ObjectId = o.OId;
            Object = o;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
        }

        public override string ToString() => $"{EventType} {ObjectId}.";
    }
}
