using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Observable
{
    /// <summary>
    /// Base class that implements <see cref="IUnifiedTypeDriver{T}"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class UnifiedTypeDriverBase<T> : IUnifiedTypeDriver<T>, IDeserializationDriver<T>, IObjectExportTypeDriver<T>
    {
        readonly bool _isFinalType;

        /// <summary>
        /// Initializes a new unified driver.
        /// </summary>
        /// <param name="isFinalType">States whether this type can have subordinate types or not.</param>
        protected UnifiedTypeDriverBase( bool isFinalType = true )
        {
            _isFinalType = isFinalType;
        }

        /// <summary>
        /// Gets this unified driver as the deserialization driver.
        /// </summary>
        public IDeserializationDriver<T> DeserializationDriver => this;

        /// <summary>
        /// Gets this unified driver as the export driver.
        /// </summary>
        public IObjectExportTypeDriver<T> ExportDriver => this;

        IDeserializationDriver IUnifiedTypeDriver.DeserializationDriver => this;

        IObjectExportTypeDriver IUnifiedTypeDriver.ExportDriver => this;

        /// <summary>
        /// Gets the type handled.
        /// </summary>
        public Type Type => typeof( T );

        bool IObjectExportTypeDriver.IsDefaultBehavior => false;

        IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => Array.Empty<PropertyInfo>();

        object? IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => ReadInstance( r, readInfo, mustRead );

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
        /// Reads the data and returns the value or instantiates a new object.
        /// </summary>
        /// <param name="r">The reader.</param>
        /// <param name="readInfo">
        /// The type based information (with versions and ancestors) of the type if it has been written by
        /// the Type itself.
        /// Null if the type has been previously written by an external driver.
        /// </param>
        /// <returns>The value or new instance.</returns>
        public abstract T ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead );

    }
}
