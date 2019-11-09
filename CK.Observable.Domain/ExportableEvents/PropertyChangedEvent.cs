namespace CK.Observable
{
    public class PropertyChangedEvent : ObservableEvent
    {
        public ObservableObjectId ObjectId { get; }

        public ObservableObject Object { get; }

        public int PropertyId { get; }

        public string PropertyName { get; }

        public object Value { get; }

        public PropertyChangedEvent( ObservableObject o, int propertyId, string propertyName, object value )
            : base( ObservableEventType.PropertyChanged )
        {
            ObjectId = o.OId;
            Object = o;
            PropertyId = propertyId;
            PropertyName = propertyName;
            Value = value;
        }

        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            e.Target.EmitInt32( PropertyId );
            ExportEventObject( e, Value );
        }

        public override string ToString() => $"{EventType} {ObjectId}.{PropertyName} = {Value ?? "null"}.";

    }


}
