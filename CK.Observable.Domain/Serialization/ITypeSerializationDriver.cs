using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Extends the <see cref="IObjectExportTypeDriver"/>.
    /// This is just becaus I'm in a hurry: the two conerns should be separated.
    /// </summary>
    public interface ITypeSerializationDriver : IObjectExportTypeDriver
    {
        /// <summary>
        /// Must be <see cref="TypeSerializationKind.TypeBased"/> or <see cref="TypeSerializationKind.External"/>.
        /// </summary>
        TypeSerializationKind SerializationKind { get; }

        /// <summary>
        /// Writes the type descriptor in the serializer.
        /// </summary>
        /// <param name="s">The serializer.</param>
        void WriteTypeInformation( Serializer s ); 

        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        void WriteData( Serializer w, object o );

        /// <summary>
        /// Reads the data and instanciates a new object.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">
        /// The type based information (with versions and ancestors) of the type as it has been written.
        /// Null if the type has been previously written by an external driver.
        /// </param>
        /// <returns>The new instance.</returns>
        object ReadInstance( Deserializer r, ObjectStreamReader.TypeBasedInfo readInfo );

    }
}
