using CK.BinarySerialization;
using CK.Core;
using FluentAssertions;
using System;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion(0)]
    public sealed class Car : ObservableObject
    {
        ObservableEventHandler<ObservableDomainEventArgs> _testSpeedChanged;
        ObservableEventHandler _positionChanged;
        ObservableEventHandler _powerChanged;
        Position _position;
        int _power;

        public Car( string name )
        {
            Throw.CheckNotNullArgument( name );
            Domain.Monitor.Info( $"Creating Car '{name}'." );
            Name = name;
        }

        Car( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
        {
            Name = d.Reader.ReadString();
            TestSpeed = d.Reader.ReadInt32();
            _position = d.ReadValue<Position>();
            _testSpeedChanged = new ObservableEventHandler<ObservableDomainEventArgs>( d );
        }

        public static void Write( IBinarySerializer s, in Car o )
        {
            s.Writer.Write( o.Name );
            s.Writer.Write( o.TestSpeed );
            s.WriteValue( o._position );
            o._testSpeedChanged.Write( s );
        }

        public string Name { get; }

        /// <summary>
        /// Gets or sets an automatic property: this is automatically handled (currently by PropertyChanged.Fody).
        /// The setter can be private.
        /// </summary>
        public int TestSpeed { get; set; }

        /// <summary>
        /// Defining this event is enough: it will be automatically fired whenever TestSpeed has changed.
        /// The private field MUST be a <see cref="ObservableEventHandler"/>, a <see cref="ObservableEventHandler{EventMonitoredArgs}"/>
        /// or a <see cref="ObservableEventHandler{ObservableDomainEventArgs}"/> exacly named _[propName]Changed.
        /// This is fired before <see cref="ObservableObject.PropertyChanged"/> event with property's name.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> TestSpeedChanged
        {
            add => _testSpeedChanged.Add( value );
            remove => _testSpeedChanged.Remove( value );
        }

        /// <summary>
        /// Gets or sets a property with a specific setter, skipping PropertyChanged.Fody weaving.
        /// The [PropertyChanged.DoNotNotify] skips the weaving: the protected OnPropertyChanged must
        /// manually be called.
        /// </summary>
        [PropertyChanged.DoNotNotify]
        public Position Position
        {
            get => _position;
            set
            {
                if( _position != value )
                {
                    _position = value;
                    OnPropertyChanged( nameof( Position ), value );
                }
            }
        }

        public event SafeEventHandler PositionChanged
        {
            add => _positionChanged.Add( value, nameof( PositionChanged ) );
            remove => _positionChanged.Remove( value );
        }

        bool _onPowerChanged;

        /// <summary>
        /// Gets or sets a property with a specific setter.
        /// The PropertyChanged.Fody magically tracks the private field set.
        /// </summary>
        public int Power
        {
            get => _power;
            set
            {
                if( _power != value )
                {
                    _onPowerChanged = false;
                    Domain.Monitor.Info( "Before Power setting." );
                    _power = value;
                    Domain.Monitor.Info( "After Power setting." );
                    _onPowerChanged.Should().BeTrue();
                }
            }
        }

        // This is called by PropertyChanged.Fody.
        void OnPowerChanged()
        {
            Domain.Monitor.Info( "Power set." );
            _onPowerChanged = true;
        }

        public event SafeEventHandler PowerChanged
        {
            add => _powerChanged.Add( value );
            remove => _powerChanged.Remove( value );
        }


        public Mechanic? CurrentMechanic { get; set; }

        public override string ToString() => $"'Car {Name}'";

        /// <summary>
        /// This is called by PropertyChanged.Fody.
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        void OnCurrentMechanicChanged( object before, object after )
        {
            if( Domain.CurrentTransactionStatus.IsRegular() )
            {
                Domain.Monitor.Info( $"{ToString()} is now repaired by: {CurrentMechanic?.ToString() ?? "null"}." );
                if( CurrentMechanic != null ) CurrentMechanic.CurrentCar = this;
                else ((Mechanic)before).CurrentCar = null;
            }
        }

    }
}
