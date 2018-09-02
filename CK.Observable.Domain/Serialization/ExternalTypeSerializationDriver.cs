using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Base implementation to support binary serialization for types that are
    /// are not serializable themselves.
    /// </summary>
    public abstract class ExternalTypeSerializationDriver : ITypeSerializationDriver
    {
        /// <summary>
        /// Initializes a new driver for a type that does not support
        /// serialization on its own.
        /// </summary>
        /// <param name="t">The type.</param>
        protected ExternalTypeSerializationDriver( Type t )
        {
            Type = t;
        }

        /// <summary>
        /// Gets the type that this driver handles.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Always <see cref="TypeSerializationKind.External"/>.
        /// </summary>
        public TypeSerializationKind SerializationKind => TypeSerializationKind.External;

        object ITypeSerializationDriver.ReadInstance( Deserializer r, ObjectStreamReader.TypeBasedInfo readInfo )
        {
            return ReadInstance( r.Reader, readInfo );
        }

        /// <summary>
        /// Reads the data and instanciates a new object.
        /// </summary>
        /// <param name="r">The reader.</param>
        /// <param name="readInfo">
        /// The type based information (with versions and ancestors) of the type if it has been written by
        /// the Type itself.
        /// Null if the type has been previously written by an external driver.
        /// </param>
        /// <returns>The new instance.</returns>
        public abstract object ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeBasedInfo readInfo );

        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        public abstract void WriteData( Serializer w, object o );

        /// <summary>
        /// Writes the type descriptor in the serializer.
        /// </summary>
        /// <param name="s">The serializer.</param>
        public void WriteTypeInformation( Serializer s )
        {
            s.DoWriteSimpleType( Type );
        }
    }

}
