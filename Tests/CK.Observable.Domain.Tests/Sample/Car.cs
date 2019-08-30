using CK.Core;
using System;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion(0)]
    public class Car : ObservableObject
    {
        public Car( string name )
        {
            DomainMonitor.Info( $"Creating Car '{name}'." );
            Name = name;
        }

        public Car( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            Name = r.ReadNullableString();
            Speed = r.ReadInt32();
            Position = (Position)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteNullableString( Name );
            w.Write( Speed );
            w.WriteObject( Position );
        }

        public string Name { get; }

        public int Speed { get; set; }

        /// <summary>
        /// Defining this event is enough: it will be automatically fired
        /// whenever Position has changed.
        /// Its type MUST be EventHandler.
        /// This is fired before INotifyPropertyChanged.PropertyChanged named event.
        /// </summary>
        public event EventHandler PositionChanged;

        public Position Position { get; set; }

        public Mechanic CurrentMechanic { get; set; }

        public override string ToString() => $"'Car {Name}'";

        void OnCurrentMechanicChanged( object before, object after )
        {
            if( IsDeserializing ) return;
            DomainMonitor.Info( $"{ToString()} is now repaired by: {CurrentMechanic?.ToString() ?? "null"}." );
            if( CurrentMechanic != null ) CurrentMechanic.CurrentCar = this;
            else ((Mechanic)before).CurrentCar = null;
        }

    }
}
