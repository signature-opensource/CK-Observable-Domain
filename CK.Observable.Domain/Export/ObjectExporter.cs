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
        readonly IExporterResolver _drivers;

        public ObjectExporter( IObjectExporterTarget target, IExporterResolver drivers = null )
        {
            if( target == null ) throw new ArgumentNullException( nameof( target ) );
            _target = target;
            _seen = new Dictionary<object, int>( PureObjectRefEqualityComparer<object>.Default );
            _drivers = drivers ?? ExporterRegistry.Default;
        }

        public static void ExportRootList( IObjectExporterTarget target, IEnumerable objects, IExporterResolver drivers = null )
        {
            var e = new ObjectExporter( target, drivers );
            e._seen.Add( objects, 0 );
            e.ExportList( 0, objects );
        }

        public IObjectExporterTarget Target => _target;

        public IExporterResolver Drivers => _drivers;

        public void ExportList( int num, IEnumerable list )
        {
            Target.EmitStartObject( num, ObjectExportedKind.List );
            foreach( var item in list ) ExportObject( item );
            Target.EmitEndObject( num, ObjectExportedKind.List );
        }

        public void ExportMap<TKey,TValue>( int num, IEnumerable<KeyValuePair<TKey, TValue>> map, IObjectExportTypeDriver<TKey> keyExporter = null, IObjectExportTypeDriver<TValue> valueExporter = null )
        {
            Target.EmitStartObject( num, ObjectExportedKind.Map );
            foreach( var kv in map )
            {
                Target.EmitStartMapEntry();
                if( keyExporter == null ) ExportObject( kv.Key );
                else Export( kv.Key, keyExporter );
                if( valueExporter == null ) ExportObject( kv.Value );
                else Export( kv.Value, valueExporter );
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

        public void Reset()
        {
            Target.ResetContext();
            _seen.Clear();
        }

        public void Export<T>( T o, IObjectExportTypeDriver<T> typedExporter )
        {
            if( typedExporter == null ) throw new ArgumentNullException( nameof( typedExporter ) );
            if( o == null )
            {
                _target.EmitNull();
                return;
            }
            int idxSeen = -1;
            var actualType = o.GetType();
            if( actualType.IsClass )
            {
                if( _seen.TryGetValue( o, out var num ) )
                {
                    _target.EmitReference( num );
                    return;
                }
                idxSeen = _seen.Count;
                _seen.Add( o, _seen.Count );
                if( actualType == typeof( object ) )
                {
                    _target.EmitEmptyObject( idxSeen );
                    return;
                }
            }
            typedExporter.Export( o, idxSeen, this );
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
                _seen.Add( o, idxSeen );
                if( t == typeof( object ) )
                {
                    _target.EmitEmptyObject( idxSeen );
                    return;
                }
            }
            IObjectExportTypeDriver driver = (o is IKnowMyExportDriver drv ? drv.ExportDriver : null )
                                                ?? _drivers.FindDriver( t );
            if( driver == null )
            {
                throw new InvalidOperationException( $"Type '{t.FullName}' is not exportable." );
            }
            driver.Export( o, idxSeen, this );
        }
    }
}
