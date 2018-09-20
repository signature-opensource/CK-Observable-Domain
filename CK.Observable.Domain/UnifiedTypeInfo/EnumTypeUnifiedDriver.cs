using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class EnumTypeUnifiedDriver<T,TU> : IUnifiedTypeDriver<T>, ITypeSerializationDriver<T>, IDeserializationDriver<T>, IObjectExportTypeDriver<T>
        where T : Enum
    {
        readonly IUnifiedTypeDriver<TU> _underlyingTypeDriver;

        public EnumTypeUnifiedDriver( IUnifiedTypeDriver<TU> underlyingTypeDriver )
        {
            if( underlyingTypeDriver == null ) throw new ArgumentNullException( nameof( underlyingTypeDriver ) );
            if( Enum.GetUnderlyingType( typeof( T ) ) != underlyingTypeDriver.CheckValidFullDriver() )
            {
                throw new ArgumentException( $"Invalid underlying type exporter '{underlyingTypeDriver}' for enum {typeof( T ).Name}.", nameof( underlyingTypeDriver ) );
            }
            _underlyingTypeDriver = underlyingTypeDriver;
        }

        public ITypeSerializationDriver<T> SerializationDriver => this;

        public IDeserializationDriver<T> DeserializationDriver => this;

        public IObjectExportTypeDriver<T> ExportDriver => this;

        public Type Type { get; }

        Type IObjectExportTypeDriver.BaseType => Type;

        IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => Array.Empty<PropertyInfo>();

        public void Export( object o, int num, ObjectExporter exporter ) => _underlyingTypeDriver.ExportDriver.Export( (TU)o, num, exporter );

        void IObjectExportTypeDriver<T>.Export( T o, int num, ObjectExporter exporter ) => Export( o, num, exporter );

        void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

        public void WriteData( BinarySerializer w, object o ) => _underlyingTypeDriver.SerializationDriver.WriteData( w, (TU)o );

        void ITypeSerializationDriver<T>.WriteData( BinarySerializer w, T o ) => WriteData( w, o );

        public object ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => _underlyingTypeDriver.DeserializationDriver.ReadInstance( r, readInfo );

        T IDeserializationDriver<T>.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => (T)ReadInstance( r, readInfo );

        string IDeserializationDriver.AssemblyQualifiedName => Type.AssemblyQualifiedName;

        ITypeSerializationDriver IUnifiedTypeDriver.SerializationDriver => this;

        IDeserializationDriver IUnifiedTypeDriver.DeserializationDriver => this;

        IObjectExportTypeDriver IUnifiedTypeDriver.ExportDriver => this;
    }
}
