using CK.Core;
using System;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion(0)]
    public class Car : ObservableObject
    {
        ObservableEventHandler<ObservableDomainEventArgs> _testSpeedChanged;

        public Car( string name )
        {
            Domain.Monitor.Info( $"Creating Car '{name}'." );
            Name = name;
        }

        protected Car( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading().Reader;
            Name = r.ReadNullableString();
            TestSpeed = r.ReadInt32();
            Position = (Position)r.ReadObject();
            _testSpeedChanged = new ObservableEventHandler<ObservableDomainEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            w.WriteNullableString( Name );
            w.Write( TestSpeed );
            w.WriteObject( Position );
            _testSpeedChanged.Write( w );
        }

        public string Name { get; }

        public int TestSpeed { get; set; }

        /// <summary>
        /// Defining this event is enough: it will be automatically fired whenever TestSpeed has changed.
        /// The private field MUST be a <see cref="ObservableEventHandler"/>, a <see cref="ObservableEventHandler{EventMonitoredArgs}"/>
        /// or a <see cref="ObservableEventHandler{ObservableDomainEventArgs}"/> exacly named _[propName]Changed.
        /// This is fired before <see cref="ObservableObject.PropertyChanged"/> event with property's name.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> TestSpeedChanged
        {
            add => _testSpeedChanged.Add( value, nameof( TestSpeedChanged ) );
            remove => _testSpeedChanged.Remove( value );
        }

        /// <summary>
        /// Defining this event is enough: it will be automatically fired whenever Position has changed.
        /// Its type MUST be EventHandler BUT, a SafeEventHandler should be used whenever possible.
        /// This is fired before <see cref="ObservableObject.PropertyChanged"/> event with property's name.
        /// </summary>
        #pragma warning disable 67 // The event 'PositionChanged' is never used.
        public event EventHandler PositionChanged;
        #pragma warning restore 67

        public Position Position { get; set; }

        public Mechanic CurrentMechanic { get; set; }

        public override string ToString() => $"'Car {Name}'";

        void OnCurrentMechanicChanged( object before, object after )
        {
            if( Domain.IsDeserializing ) return;
            Domain.Monitor.Info( $"{ToString()} is now repaired by: {CurrentMechanic?.ToString() ?? "null"}." );
            if( CurrentMechanic != null ) CurrentMechanic.CurrentCar = this;
            else ((Mechanic)before).CurrentCar = null;
        }

    }
}
