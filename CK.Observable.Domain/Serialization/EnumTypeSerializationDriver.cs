using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class EnumTypeSerializationDriver : ITypeSerializationDriver
    {
        public EnumTypeSerializationDriver( Type t )
        {
            Debug.Assert( t.IsEnum );
            Type = t;
        }

        public TypeSerializationKind SerializationKind => TypeSerializationKind.External;

        public Type Type { get; }

        public bool IsExportable => true;

        public IReadOnlyList<PropertyInfo> ExportableProperties => Array.Empty<PropertyInfo>();

        public void Export( object o, int num, ObjectExporter exporter )
        {
            Debug.Assert( num == -1 );
            exporter.Target.EmitInt32( (int)o );
        }

        public void WriteTypeInformation( BinarySerializer s )
        {
            s.WriteSimpleType( Type );
        }

        public void WriteData( BinarySerializer w, object o )
        {
            w.Write( (int)o );
        }

        public object ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
        {
            return r.Reader.ReadInt32();
        }
    }
}
