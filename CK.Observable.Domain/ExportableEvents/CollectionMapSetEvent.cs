namespace CK.Observable
{
    public class CollectionMapSetEvent : ObservableEvent, ICollectionEvent
    {
        public ObservableObject.Id ObjectId { get; }

        public ObservableObject Object { get; }

        public object Key { get; }

        public object Value { get; }

        public CollectionMapSetEvent( ObservableObject o, object key, object value )
            : base( ObservableEventType.CollectionMapSet )
        {
            ObjectId = o.ObjectId;
            Object = o;
            Key = key;
            Value = value;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            ExportEventObject( e, Key );
            ExportEventObject( e, Value );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Key}] = {Value ?? "null"}";
    }
}
