using System.Collections.Generic;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersionAttribute(0)]
    public sealed class Garage : ObservableObject
    {
        readonly ObservableDictionary<Car, Car> _replacementCars;

        public Garage()
        {
            Employees = new ObservableList<Person>();
            Cars = new ObservableList<Car>();
            _replacementCars = new ObservableDictionary<Car, Car>();
        }

        Garage( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            CompanyName = r.ReadNullableString();
            Employees = (ObservableList<Person>)r.ReadObject();
            Cars = (ObservableList<Car>)r.ReadObject();
            _replacementCars = (ObservableDictionary<Car, Car>)r.ReadObject();
        }

        void Write( IBinarySerializer s )
        {
            s.WriteNullableString( CompanyName );
            s.WriteObject( Employees );
            s.WriteObject( Cars );
            s.WriteObject( _replacementCars );
        }

        public string CompanyName { get; set; }

        public IList<Person> Employees { get; }

        public ObservableList<Car> Cars { get; }

        public IDictionary<Car, Car> ReplacementCar => _replacementCars;
    }
}
