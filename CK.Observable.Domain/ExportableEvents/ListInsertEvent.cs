namespace CK.Observable
{
    public class ListInsertEvent : ObservableEvent, ICollectionEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public object Item { get; }

        public ListInsertEvent( ObservableObject o, int index, object item )
            : base( ObservableEventType.ListInsert )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
            Item = item;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
            e.Target.EmitInt32( Index );
            ExportEventObject( e, Item );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}] = {Item}.";

    }


}
