using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface ITypeSerializationDriver<T> : ITypeSerializationDriver
    {
        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        void WriteData( Serializer w, T o );

        /// <summary>
        /// Reads the data and instanciates a new object.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">
        /// The type based information (with versions and ancestors) of the type as it has been written.
        /// Null if the type has been previously written by an external driver.
        /// </param>
        /// <returns>The new instance.</returns>
        new T ReadInstance( Deserializer r, ObjectStreamReader.TypeBasedInfo readInfo );

        /// <summary>
        /// Exports an instance. <see cref="IsExportable"/> must be true otherwise a <see cref="NotSupportedException"/>
        /// must be thrown.
        /// </summary>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="o">The instance. Must not ne null.</param>
        /// <param name="exporter">The exporter.</param>
        void Export( int num, T o, ObjectExporter exporter );

    }
}
