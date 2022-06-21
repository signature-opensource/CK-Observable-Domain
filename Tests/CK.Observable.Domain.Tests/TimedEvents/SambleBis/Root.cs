using CK.Core;
using System;

namespace CK.Observable.Domain.Tests
{
    [SerializationVersion( 0 )]
    class Root : ObservableRootObject
    {
        public ObservableList<ObservableObject> Objects { get; }

        public ObservableList<ObservableTimedEventBase> TimedEvents { get; }

        public Root()
        {
            Objects = new ObservableList<ObservableObject>();
            TimedEvents = new ObservableList<ObservableTimedEventBase>();
        }

        Root( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            Objects = r.ReadObject<ObservableList<ObservableObject>>();
            TimedEvents = r.ReadObject<ObservableList<ObservableTimedEventBase>>();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in Root o )
        {
            w.WriteObject( o.Objects );
            w.WriteObject( o.TimedEvents );
        }

        public void RemindFromPool( DateTime dueTime, SafeEventHandler<ObservableReminderEventArgs> callback )
        {
            Domain.Remind( dueTime, callback );
        }

    }
}
