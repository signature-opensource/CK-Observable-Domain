namespace CK.Observable
{
    public class ListSetAtEvent : ObservableEvent, ICollectionEvent
    {
        public ObservableObjectId ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public object Value { get; }


        public ListSetAtEvent( ObservableObject o, int index, object value )
            : base( ObservableEventType.ListSetAt )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
            Value = value;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            e.Target.EmitInt32( Index );
            ExportEventObject( e, Value );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}] = {Value ?? "null"}.";

    }


}
