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
    public abstract class ExternalTypeSerializationDriver<T> : ITypeSerializationDriver<T>
    {
        /// <summary>
        /// Gets the type that this driver handles.
        /// </summary>
        public Type Type => typeof( T );

        /// <summary>
        /// Always <see cref="TypeSerializationKind.External"/>.
        /// </summary>
        public TypeSerializationKind SerializationKind => TypeSerializationKind.External;

        object ITypeSerializationDriver.ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
        {
            return ReadInstance( r.Reader, readInfo );
        }

        T ITypeSerializationDriver<T>.ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
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
        public abstract T ReadInstance( ObjectStreamReader r, ObjectStreamReader.TypeReadInfo readInfo );

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o )
        {
            WriteData( w, (T)o );
        }

        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        public abstract void WriteData( BinarySerializer w, T o );

        /// <summary>
        /// Writes the type descriptor in the serializer.
        /// </summary>
        /// <param name="s">The serializer.</param>
        public void WriteTypeInformation( BinarySerializer s )
        {
            s.WriteSimpleType( Type );
        }

        /// <summary>
        /// Gets the property that must be exported.
        /// Defaults to empty.
        /// </summary>
        public virtual IReadOnlyList<PropertyInfo> ExportableProperties => Array.Empty<PropertyInfo>();

        /// <summary>
        /// Gets whether <see cref="Export"/> can be called: this driver knows hox to export
        /// instances of its type.
        /// </summary>
        public abstract bool IsExportable { get; }

        void IObjectExportTypeDriver.Export(object o, int num, ObjectExporter exporter) => Export( num, (T)o, exporter );

        /// <summary>
        /// Exports an instance. <see cref="IsExportable"/> must be true otherwise a <see cref="NotSupportedException"/>
        /// must be thrown.
        /// </summary>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="o">The object instance. Must not ne null.</param>
        /// <param name="exporter">The exporter.</param>
        public abstract void Export( int num, T o, ObjectExporter exporter );

    }

}
