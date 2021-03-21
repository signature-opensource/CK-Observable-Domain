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

        Root( IBinaryDeserializer r, TypeReadInfo info )
            : base( RevertSerialization.Default )
        {
            Objects = (ObservableList<ObservableObject>)r.ReadObject()!;
            TimedEvents = (ObservableList<ObservableTimedEventBase>)r.ReadObject()!;
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( Objects );
            w.WriteObject( TimedEvents );
        }

        public void RemindFromPool( DateTime dueTime, SafeEventHandler<ObservableReminderEventArgs> callback )
        {
            Domain.Remind( dueTime, callback );
        }

    }
}
