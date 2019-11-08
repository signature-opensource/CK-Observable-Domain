namespace CK.Observable
{
    public class ListRemoveAtEvent : ObservableEvent, ICollectionEvent
    {
        public ObservableObject.Id ObjectId { get; }

        public ObservableObject Object { get; }

        public int Index { get; }

        public ListRemoveAtEvent( ObservableObject o, int index )
            : base( ObservableEventType.ListRemoveAt )
        {
            ObjectId = o.ObjectId;
            Object = o;
            Index = index;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            e.Target.EmitInt32( Index );
        }

        public override string ToString() => $"{EventType} {ObjectId}[{Index}].";

    }


}
