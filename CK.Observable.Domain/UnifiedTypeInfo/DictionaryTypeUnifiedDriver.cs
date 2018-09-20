using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class DictionaryTypeUnifiedDriver<TKey,TValue> : IUnifiedTypeDriver<Dictionary<TKey,TValue>>
    {
        public DictionaryTypeUnifiedDriver( IUnifiedTypeDriver<TKey> keyDriver, IUnifiedTypeDriver<TValue> valueDriver )
        {
            SerializationDriver = new DictionaryTypeSerializer<TKey,TValue>( keyDriver.SerializationDriver, valueDriver.SerializationDriver );
            DeserializationDriver = new DictionaryTypeDeserializer<TKey,TValue>( keyDriver.DeserializationDriver, valueDriver.DeserializationDriver );
            ExportDriver = new MapTypeExportDriver<TKey,TValue>( keyDriver.ExportDriver, valueDriver.ExportDriver );
        }

        public ITypeSerializationDriver<Dictionary<TKey, TValue>> SerializationDriver { get; }

        public IDeserializationDriver<Dictionary<TKey, TValue>> DeserializationDriver { get; }

        public IObjectExportTypeDriver<Dictionary<TKey, TValue>> ExportDriver { get; }

        ITypeSerializationDriver IUnifiedTypeDriver.SerializationDriver => SerializationDriver;

        IDeserializationDriver IUnifiedTypeDriver.DeserializationDriver => DeserializationDriver;

        IObjectExportTypeDriver IUnifiedTypeDriver.ExportDriver => ExportDriver;
    }
}
