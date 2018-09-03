using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ObjectExporter
    {
        readonly IObjectExporterTarget _target;
        readonly Dictionary<object, int> _seen;

        class PureRefEquality : IEqualityComparer<object>
        {
            public new bool Equals( object x, object y ) => ReferenceEquals( x, y );

            public int GetHashCode( object obj ) => obj.GetHashCode();
        }

        static readonly PureRefEquality RefEquality = new PureRefEquality();

        public ObjectExporter( IObjectExporterTarget target )
        {
            _target = target;
            _seen = new Dictionary<object, int>( RefEquality );
        }

        public IObjectExporterTarget Target => _target;

        public void ExportList( int num, IEnumerable list )
        {
            Target.EmitStartObject( num, ObjectExportedKind.List );
            foreach( var item in list ) ExportObject( item );
            Target.EmitEndObject( num, ObjectExportedKind.List );
        }

        public void ExportMap<TKey,TValue>( int num, IEnumerable<KeyValuePair<TKey, TValue>> map )
        {
            Target.EmitStartObject( num, ObjectExportedKind.Map );
            foreach( var kv in map )
            {
                Target.EmitStartMapEntry();
                ExportObject( kv.Key );
                ExportObject( kv.Value );
                Target.EmitEndMapEntry();
            }
            Target.EmitEndObject( num, ObjectExportedKind.Map );
        }

        public void ExportObject( object o )
        {
            if( o == null )
            {
                _target.EmitNull();
                return;
            }
            Type t = o.GetType();
            int idxSeen = -1;
            if( t.IsClass && t != typeof(string) )
            {
                if( _seen.TryGetValue( o, out var num ) )
                {
                    _target.EmitReference( num );
                    return;
                }
                idxSeen = _seen.Count;
                _seen.Add( o, _seen.Count );
                if( t == typeof( object ) )
                {
                    _target.EmitEmptyObject( idxSeen );
                    return;
                }
            }
            IObjectExportTypeDriver driver = SerializableTypes.FindDriver( t, TypeSerializationKind.None );
            if( !driver.IsExportable )
            {
                throw new Exception( $"Type {t.Name} is not exportable." );
            }
            driver.Export(o, idxSeen, this);
        }
    }
}
