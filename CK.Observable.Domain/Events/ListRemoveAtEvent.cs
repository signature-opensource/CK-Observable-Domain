namespace CK.Observable
{
    public class ListRemoveAtEvent : ObservableEvent, ICollectionEvent
    {
        public int ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public ListRemoveAtEvent( ObservableObject o, int index )
            : base( ObservableEventType.ListRemoveAt )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId );
            e.Target.EmitInt32( Index );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}].";

    }


}
