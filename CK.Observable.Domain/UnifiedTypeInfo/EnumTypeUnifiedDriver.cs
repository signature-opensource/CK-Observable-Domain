using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class EnumTypeUnifiedDriver : IUnifiedTypeDriver, ITypeSerializationDriver, IDeserializationDriver, IObjectExportTypeDriver
    {
        public EnumTypeUnifiedDriver( Type t )
        {
            Debug.Assert( t.IsEnum );
            Type = t;
        }

        public ITypeSerializationDriver SerializationDriver => this;

        public IDeserializationDriver DeserializationDriver => this;

        public IObjectExportTypeDriver ExportDriver => this;

        public Type Type { get; }

        IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => Array.Empty<PropertyInfo>();

        void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter )
        {
            Debug.Assert( num == -1 );
            exporter.Target.EmitInt32( (int)o );
        }

        void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s )
        {
            s.WriteSimpleType( Type );
        }

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o )
        {
            w.Write( (int)o );
        }

        object IDeserializationDriver.ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
        {
            return r.Reader.ReadInt32();
        }

        string IDeserializationDriver.AssemblyQualifiedName => Type.AssemblyQualifiedName;

    }
}
