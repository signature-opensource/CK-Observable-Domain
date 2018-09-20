using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public static class AutoTypeRegistry
    {
        static readonly ConcurrentDictionary<Type, IUnifiedTypeDriver> _drivers;
        static readonly Type[] _ctorParameters = new Type[] { typeof( IBinaryDeserializerContext ) };
        static readonly Type[] _writeParameters = new Type[] { typeof( BinarySerializer ) };
        static readonly Type[] _exportParameters = new Type[] { typeof( int ), typeof( ObjectExporter ) };
        static readonly Type[] _exportBaseParameters = new Type[] { typeof( int ), typeof( ObjectExporter ), typeof( IReadOnlyList<ExportableProperty> ) };

        static AutoTypeRegistry()
        {
            _drivers = new ConcurrentDictionary<Type, IUnifiedTypeDriver>();
        }

        /// <summary>
        /// Type based unified driver.
        /// </summary>
        class AutoTypeDriver : IUnifiedTypeDriver, ITypeSerializationDriver, IDeserializationDriver, IObjectExportTypeDriver
        {
            readonly Type _type;
            readonly AutoTypeDriver _baseType;
            readonly AutoTypeDriver[] _typePath;

            // Serialization.
            readonly MethodInfo _write;
            /// <summary>
            /// The version from the <see cref="SerializationVersionAttribute"/>.
            /// </summary>
            readonly int _version;

            // Deserialization.
            readonly ConstructorInfo _ctor;

            // Export.
            readonly MethodInfo _exporter;
            readonly MethodInfo _exporterBase;
            readonly IReadOnlyList<PropertyInfo> _exportableProperties;
            readonly bool _isExportable;

            /// <summary>
            /// Gets the type itself.
            /// </summary>
            public Type Type => _type;

            public ITypeSerializationDriver SerializationDriver => _write != null ? this : null;

            public IDeserializationDriver DeserializationDriver => _ctor != null ? this : null;

            public IObjectExportTypeDriver ExportDriver => _isExportable ? this : null;

            IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => _exportableProperties;

            Type IObjectExportTypeDriver.BaseType => Type;

            string IDeserializationDriver.AssemblyQualifiedName => _type.AssemblyQualifiedName;

            /// <summary>
            /// Invokes the deserialization constructor.
            /// </summary>
            /// <param name="r">The deserializer.</param>
            /// <param name="readInfo">
            /// The type based information (with versions and ancestors) of the type as it has been written.
            /// Null if the type has been previously written by an external driver.
            /// </param>
            /// <returns>The new instance.</returns>
            object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) 
            {
                return DoReadInstance( r, readInfo );
            }

            protected object DoReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo )
            {
                var o = r.ImplementationServices.CreateUninitializedInstance( Type, readInfo.IsTrackedObject );
                var ctx = r.ImplementationServices.PushConstructorContext( readInfo );
                _ctor.Invoke( o, new object[] { ctx } );
                r.ImplementationServices.PopConstructorContext();
                return o;
            }

            /// <summary>
            /// Writes types in <see cref="TypePath"/> that has not been
            /// written yet (they are unknown to the Serializer).
            /// </summary>
            /// <param name="s">The serializer.</param>
            void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s )
            {
                var tInfo = this;
                while( s.DoWriteSimpleType( tInfo?.Type, null ) )
                {
                    Debug.Assert( tInfo._version >= 0 );
                    s.WriteSmallInt32( tInfo._version );
                    tInfo = tInfo._baseType;
                }
            }

            /// <summary>
            /// Calls the Write methods on <see cref="TypePath"/>.
            /// </summary>
            /// <param name="w">The serializer.</param>
            /// <param name="o">The object instance.</param>
            void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => DoWriteData( w, o );

            protected void DoWriteData( BinarySerializer w, object o )
            {
                var parameters = new object[] { w };
                foreach( var t in _typePath )
                {
                    t._write.Invoke( o, parameters );
                }
            }

            void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter )
            {
                DoExport( o, num, exporter );
            }

            protected void DoExport( object o, int num, ObjectExporter exporter )
            {
                if( _exporter != null )
                {
                    _exporter.Invoke( o, new object[] { num, exporter } );
                }
                else if( _exporterBase != null )
                {
                    var props = _exportableProperties.Select( p => new ExportableProperty( p.DeclaringType, p.Name, p.GetValue( o ) ) )
                                                     .ToArray();
                    _exporterBase.Invoke( o, new object[] { num, exporter, props } );
                }
                else
                { 
                    if( _exportableProperties.Count == 0 )
                    {
                        exporter.Target.EmitEmptyObject( num );
                    }
                    else
                    {
                        exporter.Target.EmitStartObject( num, ObjectExportedKind.Object );
                        foreach( var p in _exportableProperties )
                        {
                            exporter.ExportNamedProperty( p.Name, p.GetValue( o ) );
                        }
                        exporter.Target.EmitEndObject( num, ObjectExportedKind.Object );
                    }
                }
            }

            internal AutoTypeDriver( Type t, AutoTypeDriver baseType )
            {
                GetAndCheckTypeAutoParts( t, out _version, out _ctor, out _write, out _exporter, out _exporterBase );
                _type = t;
                _baseType = baseType;
                if( baseType != null )
                {
                    var p = new AutoTypeDriver[baseType._typePath.Length + 1];
                    Array.Copy( baseType._typePath, p, baseType._typePath.Length );
                    p[baseType._typePath.Length] = this;
                    _typePath = p;
                }
                else _typePath = new[] { this };

                _exportableProperties = Array.Empty<PropertyInfo>();
                if( !Type.GetCustomAttributes<NotExportableAttribute>().Any() )
                {
                    _isExportable = true;
                    if( _exporter == null )
                    {
                        if( _exporterBase == null )
                        {
                            _exporterBase = baseType?._exporterBase;
                        }
                        _exportableProperties = Type.GetProperties().Where( p => p.GetIndexParameters().Length == 0
                                                            && !p.GetCustomAttributes<NotExportableAttribute>().Any() )
                                                .ToArray();
                    }
                }
            }
        }

        class AutoTypeDriver<T> : AutoTypeDriver, ITypeSerializationDriver<T>, IDeserializationDriver<T>, IObjectExportTypeDriver<T>
        {
            public AutoTypeDriver( AutoTypeDriver baseType )
                : base( typeof(T), baseType )
            {
            }

            void IObjectExportTypeDriver<T>.Export( T o, int num, ObjectExporter exporter )
            {
                DoExport( o, num, exporter );
            }

            public T ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo )
            {
                return (T)DoReadInstance( r, readInfo );
            }

            void ITypeSerializationDriver<T>.WriteData( BinarySerializer w, T o ) => DoWriteData( w, o );
        }

        /// <summary>
        /// Finds driver for a Type that may be serializable, exportable, deserializable or not.
        /// </summary>
        /// <typeparam name="T">The type for which a driver must be found.</typeparam>
        /// <returns>Null if the type is null, the driver otherwise.</returns>
        public static IUnifiedTypeDriver<T> FindDriver<T>()
        {
            return (IUnifiedTypeDriver<T>)FindDriver( typeof(T) );
        }

        /// <summary>
        /// Finds driver for a Type that may be serializable, exportable, deserializable or not.
        /// </summary>
        /// <param name="t">The type for which a driver must be found. Can be null: null is returned.</param>
        /// <returns>Null if the type is null, the driver otherwise.</returns>
        public static IUnifiedTypeDriver FindDriver( Type t )
        {
            if( t == null ) return null;
            if( _drivers.TryGetValue( t, out var info ) )
            {
                return info;
            }
            var dType = typeof( AutoTypeDriver<> ).MakeGenericType( t );
            var baseTypeInfo = t.BaseType == typeof( object ) || t.BaseType == typeof( ValueType )
                                ? null
                                : (AutoTypeDriver)FindDriver( t.BaseType );
            return _drivers.GetOrAdd( t, newType => (IUnifiedTypeDriver)Activator.CreateInstance( dType, baseTypeInfo ) );
        }

        static void GetAndCheckTypeAutoParts(
            Type t,
            out int version,
            out ConstructorInfo ctor,
            out MethodInfo write,
            out MethodInfo exporter,
            out MethodInfo exporterBase )
        {
            int? v = t.GetCustomAttribute<SerializationVersionAttribute>()?.Version;
            ctor = t.GetConstructor( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                     null,
                                     _ctorParameters,
                                     null );
            write = t.GetMethod( "Write",
                                 BindingFlags.Instance | BindingFlags.NonPublic,
                                 null,
                                 _writeParameters,
                                 null );

            if( v != null || write != null )
            {
                if( v == null ) throw new InvalidOperationException( $"Missing [{nameof( SerializationVersionAttribute )}] attribute on type '{t.Name}'." );
                Debug.Assert( v.Value >= 0 );
                if( write == null ) throw new InvalidOperationException( $"Missing 'void {t.Name}.Write({nameof( BinarySerializer )} )' method." );
                version = v.Value;
            }
            else version = -1;

            exporter = t.GetMethod( "Export",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                                    null,
                                    _exportParameters,
                                    null );

            exporterBase = t.GetMethod( "Export",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                                        null,
                                        _exportBaseParameters,
                                        null );

            if( exporter != null && exporterBase != null )
            {
                throw new InvalidOperationException( $"Ambiguous methods 'void {t.Name}.Export()'. Choose among the two the Export method that must be used." );
            }
        }

    }
}
