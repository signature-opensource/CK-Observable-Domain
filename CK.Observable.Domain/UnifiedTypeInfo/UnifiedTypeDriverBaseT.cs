using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public abstract class UnifiedTypeDriverBase<T> : IUnifiedTypeDriver<T>, ITypeSerializationDriver<T>, IDeserializationDriver<T>, IObjectExportTypeDriver<T>
    {
        readonly string _typeAlias;

        protected UnifiedTypeDriverBase( string typeAlias = null )
        {
            _typeAlias = null;
        }

        public ITypeSerializationDriver<T> SerializationDriver => this;

        public IDeserializationDriver<T> DeserializationDriver => this;

        public IObjectExportTypeDriver<T> ExportDriver => this;

        ITypeSerializationDriver IUnifiedTypeDriver.SerializationDriver => this;

        IDeserializationDriver IUnifiedTypeDriver.DeserializationDriver => this;

        IObjectExportTypeDriver IUnifiedTypeDriver.ExportDriver => this;

        public Type Type => typeof( T );

        bool IObjectExportTypeDriver.IsDefaultBehavior => false;

        IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => Array.Empty<PropertyInfo>();

        void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( typeof( T ), _typeAlias );

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => WriteData( w, (T)o );

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );

        string IDeserializationDriver.AssemblyQualifiedName => typeof( T ).AssemblyQualifiedName;

        void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter ) => Export( (T)o, num, exporter );

        Type IObjectExportTypeDriver.BaseType => Type;

        /// <summary>
        /// Exports an instance.
        /// </summary>
        /// <param name="o">The object instance. Must not ne null.</param>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="exporter">The exporter.</param>
        public abstract void Export( T o, int num, ObjectExporter exporter );

        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        public abstract void WriteData( BinarySerializer w, T o );

        /// <summary>
        /// Reads the data and returns the value or instanciates a new object.
        /// </summary>
        /// <param name="r">The reader.</param>
        /// <param name="readInfo">
        /// The type based information (with versions and ancestors) of the type if it has been written by
        /// the Type itself.
        /// Null if the type has been previously written by an external driver.
        /// </param>
        /// <returns>The value or new instance.</returns>
        public abstract T ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  );

    }
}
