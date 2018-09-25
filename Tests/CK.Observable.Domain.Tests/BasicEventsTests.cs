using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class BasicEventsTests
    {
        [Test]
        public void simple_property_changed_events()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            Car c0 = null;
            Car c1 = null;
            IReadOnlyList<ObservableEvent> events;

            events = domain.Modify( () =>
            {
                c0 = new Car( "First Car" );
                c1 = new Car( "Second Car" );
            } );
            Check( events, "NewObject 0 (Car).",
                           "NewObject 1 (Car).",
                           "NewProperty Name -> 0.",
                           "PropertyChanged 0.Name = First Car.",
                           "NewProperty Speed -> 1.",
                           "PropertyChanged 0.Speed = 0.",
                           "NewProperty Position -> 2.",
                           "PropertyChanged 0.Position = (0,0).",
                           "NewProperty CurrentMechanic -> 3.",
                           "PropertyChanged 0.CurrentMechanic = null.",
                           "PropertyChanged 1.Name = Second Car.",
                           "PropertyChanged 1.Speed = 0.",
                           "PropertyChanged 1.Position = (0,0).",
                           "PropertyChanged 1.CurrentMechanic = null." );

            events = domain.Modify( () =>
            {
                c0.Speed = 1;
            } );
            Check( events, "PropertyChanged 0.Speed = 1." );

            events = domain.Modify( () =>
            {
                c0.Speed = 78;
                c1.Position = new Position( 1.5, 2.3 );
            } );
            Check( events, "PropertyChanged 0.Speed = 78.", "PropertyChanged 1.Position = (1.5,2.3)." );
        }

        [Test]
        public void property_changed_events_use_the_last_value()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            Car c = null;
            IReadOnlyList<ObservableEvent> events;

            events = domain.Modify( () =>
            {
                c = new Car( "First Car" );
            } );
            
            events = domain.Modify( () =>
            {
                c.Position = new Position(1.0,2.0);
                c.Speed = 1;
                c.Speed = 2;
                c.Speed = 3;
                c.Position = new Position(3.0,4.0);
            } );
            Check( events, "PropertyChanged 0.Speed = 3.", "PropertyChanged 0.Position = (3,4)." );
        }

        [Test]
        public void list_changed_events()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            Car c0 = null;
            Car c1 = null;
            Garage g = null;
            IReadOnlyList<ObservableEvent> events;

            events = domain.Modify( () =>
            {
                c0 = new Car( "C1" );
                c1 = new Car( "C2" );
                g = new Garage();
            } );
            events = domain.Modify( () =>
            {
                g.Cars.Add( c0 );
                g.Cars.Insert( 0, c1 );
                g.Cars.Clear();
            } );
            Check( events, "ListInsert 4[0] = 'Car C1'.", "ListInsert 4[0] = 'Car C2'.", "CollectionClear 4." );
        }

        [Test]
        public void simple_reflexes()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            Car c = null;
            Mechanic m = null;
            Garage g = null;
            IReadOnlyList<ObservableEvent> events;
            events = domain.Modify( () =>
            {
                g = new Garage();
                c = new Car( "C" );
                m = new Mechanic( g ) { FirstName = "Jon", LastName = "Doe" };
            } );
            events = domain.Modify( () =>
            {
                m.CurrentCar = c;
            } );
            Check( events, "PropertyChanged 4.CurrentMechanic = 'Mechanic Jon Doe'.",
                           "PropertyChanged 5.CurrentCar = 'Car C'." );
            events = domain.Modify( () =>
            {
                m.CurrentCar = null;
            } );
            Check( events, "PropertyChanged 4.CurrentMechanic = null.",
                           "PropertyChanged 5.CurrentCar = null." );

            events = domain.Modify( () =>
            {
                c.CurrentMechanic = m;
            } );
            Check( events, "PropertyChanged 5.CurrentCar = 'Car C'.",
                           "PropertyChanged 4.CurrentMechanic = 'Mechanic Jon Doe'." );

        }

        static void Check( IReadOnlyList<ObservableEvent> events, params string[] e )
        {
            events.Should().HaveCount( e.Length );
            events.Select( ev => ev.ToString() ).Should().Contain( e );
        }

    }


}
