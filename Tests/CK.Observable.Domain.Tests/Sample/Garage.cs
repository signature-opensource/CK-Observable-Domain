using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersionAttribute(0)]
    public class Garage : ObservableObject
    {
        ObservableDictionary<Car, Car> _replacementCars;

        public Garage()
        {
            Employees = new ObservableList<Person>();
            Cars = new ObservableList<Car>();
            _replacementCars = new ObservableDictionary<Car, Car>();
        }

        public Garage( BinaryDeserializer d )
            : base( d )
        {
            var r = d.StartReading();
            CompanyName = r.ReadNullableString();
            r.ReadObject<ObservableList<Person>>( x => Employees = x );
            r.ReadObject( x => Cars = (ObservableList<Car>)x );
            r.ReadObject<ObservableDictionary<Car, Car>>( x => _replacementCars = x );
        }

        void Write( BinarySerializer s )
        {
            s.WriteNullableString( CompanyName );
            s.WriteObject( Employees );
            s.WriteObject( Cars );
            s.WriteObject( _replacementCars );
        }

        public string CompanyName { get; set; }

        public IList<Person> Employees { get; private set; }

        public ObservableList<Car> Cars { get; private set; }

        public IDictionary<Car, Car> ReplacementCar => _replacementCars;
    }
}
