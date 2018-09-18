using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ListTypeUnifiedDriver<T> : IUnifiedTypeDriver<List<T>>
    {
        public ListTypeUnifiedDriver( IUnifiedTypeDriver<T> itemDriver )
        {
            SerializationDriver = new ListTypeSerializer<T>( itemDriver.SerializationDriver );
            DeserializationDriver = new ListTypeDeserializer<T>( itemDriver.DeserializationDriver );
            ExportDriver = new ListTypeExportDriver<T>( itemDriver.ExportDriver );
        }

        public ITypeSerializationDriver<List<T>> SerializationDriver { get; }

        public IDeserializationDriver<List<T>> DeserializationDriver { get; }

        public IObjectExportTypeDriver<List<T>> ExportDriver { get; }

        ITypeSerializationDriver IUnifiedTypeDriver.SerializationDriver => SerializationDriver;

        IDeserializationDriver IUnifiedTypeDriver.DeserializationDriver => DeserializationDriver;

        IObjectExportTypeDriver IUnifiedTypeDriver.ExportDriver => ExportDriver;
    }
}
