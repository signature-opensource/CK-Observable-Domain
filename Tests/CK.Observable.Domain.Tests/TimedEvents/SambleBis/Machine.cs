using CK.Core;

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

        Machine( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            Clock = r.ReadObject<SuspendableClock>();
            Internal = r.ReadNullableObject<InternalObject>();
            Observable = r.ReadNullableObject<ObservableObject>();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in Machine o )
        {
            w.WriteObject( o.Clock );
            w.WriteNullableObject( o.Internal );
            w.WriteNullableObject( o.Observable );
        }
    }
}

