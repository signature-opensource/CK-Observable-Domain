using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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


        object ITypeSerializationDriver.ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
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
        public abstract object ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeReadInfo readInfo );

        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        public abstract void WriteData( BinarySerializer w, object o );

        /// <summary>
        /// Writes the type descriptor in the serializer.
        /// </summary>
        /// <param name="s">The serializer.</param>
        public void WriteTypeInformation( BinarySerializer s )
        {
            s.WriteSimpleType( Type );
        }

        /// <summary>
        /// Gets whether <see cref="Export"/> can be called: this driver knows hox to export
        /// instances of its type.
        /// </summary>
        public abstract bool IsExportable { get; }

        /// <summary>
        /// Gets the property that must be exported.
        /// This is empty for basic types.
        /// </summary>
        public abstract IReadOnlyList<PropertyInfo> ExportableProperties { get; }

        /// <summary>
        /// Exports an instance. <see cref="IsExportable"/> must be true otherwise a <see cref="NotSupportedException"/>
        /// must be thrown.
        /// </summary>
        /// <param name="o">The object instance. Must not ne null.</param>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="exporter">The exporter.</param>
        public abstract void Export(object o, int num, ObjectExporter exporter);
    }

}
