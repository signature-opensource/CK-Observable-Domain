namespace CK.Observable
{
    public class CollectionRemoveKeyEvent : ObservableEvent, ICollectionEvent
    {
        public ObservableObject.Id ObjectId { get; }

        public ObservableObject Object { get; }

        public object Key { get; }

        public CollectionRemoveKeyEvent( ObservableObject o, object key )
            : base( ObservableEventType.CollectionRemoveKey )
        {
            ObjectId = o.ObjectId;
            Object = o;
            Key = key;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            ExportEventObject( e, Key );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Key}]";
    }
}
