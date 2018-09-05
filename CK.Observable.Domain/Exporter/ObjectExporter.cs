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

        public static void ExportRootList( IObjectExporterTarget target, IEnumerable objects )
        {
            var e = new ObjectExporter( target );
            e._seen.Add( e, 0 );
            e.ExportList( 0, objects );
        }

        public static void ExportRootMap<TKey,TValue>( IObjectExporterTarget target, IEnumerable<KeyValuePair<TKey, TValue>> map )
        {
            var e = new ObjectExporter( target );
            e._seen.Add( e, 0 );
            e.ExportMap( 0, map );
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

        public void ExportNamedProperty( string name, object o )
        {
            Target.EmitPropertyName( name );
            ExportObject( o );
        }

        public void ExportProperties( IEnumerable<ExportableProperty> properties )
        {
            foreach( var p in properties )
            {
                Target.EmitPropertyName( p.Name );
                ExportObject( p.Value );
            }
        }

        public void ExportObject( object o )
        {
            switch( o )
            {
                case null:
                    {
                        _target.EmitNull();
                        return;
                    }
                case string s:
                    {
                        _target.EmitString( s );
                        return;
                    }
                case int i:
                    {
                        _target.EmitInt32( i );
                        return;
                    }
                case double d:
                    {
                        _target.EmitDouble( d );
                        return;
                    }
                case char c:
                    {
                        _target.EmitChar( c );
                        return;
                    }
                case bool b:
                    {
                        _target.EmitBool( b );
                        return;
                    }
                case uint ui:
                    {
                        _target.EmitUInt32( ui );
                        return;
                    }
                case float f:
                    {
                        _target.EmitSingle( f );
                        return;
                    }
                case DateTime d:
                    {
                        _target.EmitDateTime( d );
                        return;
                    }
                case Guid g:
                    {
                        _target.EmitGuid( g );
                        return;
                    }
                case TimeSpan ts:
                    {
                        _target.EmitTimeSpan( ts );
                        return;
                    }
            }
            Type t = o.GetType();
            int idxSeen = -1;
            if( t.IsClass )
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
            var driver = o is IKnowSerializationDriver k
                           ? k.SerializationDriver
                           : SerializableTypes.FindDriver( t, TypeSerializationKind.None );
            if( !driver.IsExportable )
            {
                throw new Exception( $"Type '{t.FullName}' is not exportable." );
            }
            driver.Export(o, idxSeen, this);
        }
    }
}
