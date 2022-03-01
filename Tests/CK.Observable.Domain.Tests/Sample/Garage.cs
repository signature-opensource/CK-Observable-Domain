using System.Collections.Generic;

namespace CK.Observable.Domain.Tests.Sample
{
    [BinarySerialization.SerializationVersionAttribute(0)]
    public sealed class Garage : ObservableObject
    {
        readonly ObservableDictionary<Car, Car> _replacementCars;

        public Garage()
        {
            Employees = new ObservableList<Person>();
            Cars = new ObservableList<Car>();
            _replacementCars = new ObservableDictionary<Car, Car>();
        }

        Garage( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            CompanyName = r.Reader.ReadNullableString();
            Employees = r.ReadObject<ObservableList<Person>>();
            Cars = r.ReadObject<ObservableList<Car>>();
            _replacementCars = r.ReadObject<ObservableDictionary<Car, Car>>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Garage o )
        {
            s.Writer.WriteNullableString( o.CompanyName );
            s.WriteObject( o.Employees );
            s.WriteObject( o.Cars );
            s.WriteObject( o._replacementCars );
        }

        public string CompanyName { get; set; }

        public IList<Person> Employees { get; }

        public ObservableList<Car> Cars { get; }

        public IDictionary<Car, Car> ReplacementCar => _replacementCars;
    }
}
