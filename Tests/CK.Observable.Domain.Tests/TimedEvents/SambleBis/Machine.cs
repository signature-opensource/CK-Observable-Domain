namespace CK.Observable.Domain.Tests
{
    [SerializationVersion( 0 )]
    class Machine : ObservableObject
    {
        public Machine()
        {
            Clock = new SuspendableClock();
            // Set it to false so that IdempotenceSerialization can be called
            // (without deactivating the clock).
            Clock.CumulateUnloadedTime = false;
        }

        public SuspendableClock Clock { get; }

        public InternalObject? Internal { get; set; }

        public ObservableObject? Observable { get; set; }

        Machine( IBinaryDeserializer r, TypeReadInfo info )
            : base( RevertSerialization.Default )
        {
            Clock = (SuspendableClock)r.ReadObject();
            Internal = (InternalObject?)r.ReadObject();
            Observable = (ObservableObject?)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Clock );
            w.WriteObject( Internal );
            w.WriteObject( Observable );
        }
    }
}

