namespace CK.Observable
{
    public class CollectionClearEvent : ObservableEvent, ICollectionEvent
    {
        public ObservableObject.Id ObjectId { get; }

        public ObservableObject Object { get; }

        public CollectionClearEvent( ObservableObject o )
            : base( ObservableEventType.CollectionClear )
        {
            ObjectId = o.ObjectId;
            Object = o;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
        }

        public override string ToString() => $"{EventType} {ObjectId}.";
    }
}
