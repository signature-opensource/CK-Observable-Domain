using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ArrayTypeUnifiedDriver<T> : IUnifiedTypeDriver<T[]>
    {
        public ArrayTypeUnifiedDriver( IUnifiedTypeDriver<T> itemDriver )
        {
            SerializationDriver = new ArrayTypeSerializer<T>( itemDriver.SerializationDriver );
            DeserializationDriver = new ArrayTypeDeserializer<T>( itemDriver.DeserializationDriver );
            ExportDriver = new EnumerableTypeExporter<T>( itemDriver.ExportDriver );
        }

        public ITypeSerializationDriver<T[]> SerializationDriver { get; }

        public IDeserializationDriver<T[]> DeserializationDriver { get; }

        public IObjectExportTypeDriver<T[]> ExportDriver { get; }

        ITypeSerializationDriver IUnifiedTypeDriver.SerializationDriver => SerializationDriver;

        IDeserializationDriver IUnifiedTypeDriver.DeserializationDriver => DeserializationDriver;

        IObjectExportTypeDriver IUnifiedTypeDriver.ExportDriver => ExportDriver;
    }
}
