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
    class UnifiedTypeRegistry
    {
        static readonly ConcurrentDictionary<Type, IUnifiedTypeDriver> _types;
        static readonly ConcurrentDictionary<string, IDeserializationDriver> _deserializations;
        static readonly Type[] _ctorParameters;
        static readonly Type[] _writeParameters;
        static readonly Type[] _exportParameters;
        static readonly Type[] _exportBaseParameters;

        static UnifiedTypeRegistry()
        {
            _types = new ConcurrentDictionary<Type, IUnifiedTypeDriver>();
            _deserializations = new ConcurrentDictionary<string, IDeserializationDriver>();
            _ctorParameters = new Type[] { typeof( BinaryDeserializer ) };
            _writeParameters = new Type[] { typeof( BinarySerializer ) };
            _exportParameters = new Type[] { typeof( int ), typeof( ObjectExporter ) };
            _exportBaseParameters = new Type[] { typeof( int ), typeof( ObjectExporter ), typeof( IReadOnlyList<ExportableProperty> ) };
            RegisterValidFullDriver( new BasicTypeDrivers.DBool() );
            RegisterValidFullDriver( new BasicTypeDrivers.DByte() );
            RegisterValidFullDriver( new BasicTypeDrivers.DChar() );
            RegisterValidFullDriver( new BasicTypeDrivers.DDateTime() );
            RegisterValidFullDriver( new BasicTypeDrivers.DDateTimeOffset() );
            RegisterValidFullDriver( new BasicTypeDrivers.DDecimal() );
            RegisterValidFullDriver( new BasicTypeDrivers.DDouble() );
            RegisterValidFullDriver( new BasicTypeDrivers.DGuid() );
            RegisterValidFullDriver( new BasicTypeDrivers.DInt16() );
            RegisterValidFullDriver( new BasicTypeDrivers.DInt32() );
            RegisterValidFullDriver( new BasicTypeDrivers.DInt64() );
            RegisterValidFullDriver( new BasicTypeDrivers.DSByte() );
            RegisterValidFullDriver( new BasicTypeDrivers.DSingle() );
            RegisterValidFullDriver( new BasicTypeDrivers.DString() );
            RegisterValidFullDriver( new BasicTypeDrivers.DTimeSpan() );
            RegisterValidFullDriver( new BasicTypeDrivers.DUInt16() );
            RegisterValidFullDriver( new BasicTypeDrivers.DUInt32() );
            RegisterValidFullDriver( new BasicTypeDrivers.DUInt64() );
        }

        /// <summary>
        /// Serializable <see cref="TypeSerializationKind.TypeBased"/> descriptor.
        /// </summary>
        public class TypeInfo : IUnifiedTypeDriver, ITypeSerializationDriver, IDeserializationDriver, IObjectExportTypeDriver
        {
            readonly Type _type;
            readonly TypeInfo _baseType;
            readonly TypeInfo[] _typePath;

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
            object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
            {
                var o = r.ImplementationServices.CreateUninitializedInstance( Type );
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
                while( s.DoWriteSimpleType( tInfo?.Type ) )
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
            void ITypeSerializationDriver.WriteData( BinarySerializer w, object o )
            {
                var parameters = new object[] { w };
                foreach( var t in _typePath )
                {
                    t._write.Invoke( o, parameters );
                }
            }

            void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter )
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

            internal TypeInfo( Type t, TypeInfo baseType )
            {
                GetAndCheckTypeSerializableParts( t, out _version, out _ctor, out _write, out _exporter, out _exporterBase );
                _type = t;
                _baseType = baseType;
                if( baseType != null )
                {
                    var p = new TypeInfo[baseType._typePath.Length + 1];
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

        /// <summary>
        /// Registers a full driver (<see cref="UnifiedTypeDriverExtension.CheckValidFullDriver"/> is called).
        /// This replaces any existing driver for the Type.
        /// </summary>
        /// <param name="driver">The driver to register.</param>
        public static void RegisterValidFullDriver( IUnifiedTypeDriver driver )
        {
            Type t = driver.CheckValidFullDriver();
            _types.AddOrUpdate( t, driver, ( type, already ) => driver );
            RegisterDeserializer( t.AssemblyQualifiedName, driver.DeserializationDriver );
        }

        /// <summary>
        /// Registers a desererializer.
        /// This replaces any existing desererializer for this name.
        /// </summary>
        /// <param name="assemblyQualifiedName">The assembly qualified name. Will be made weak thanks to <see cref="SimpleTypeFinder.WeakenAssemblyQualifiedName"/>.</param>
        /// <param name="driver">The driver to register.</param>
        public static void RegisterDeserializer(string assemblyQualifiedName, IDeserializationDriver driver)
        {
            if( String.IsNullOrWhiteSpace( assemblyQualifiedName) ) throw new ArgumentNullException( nameof( assemblyQualifiedName ) );
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            if( !SimpleTypeFinder.WeakenAssemblyQualifiedName( assemblyQualifiedName, out string weakName ) )
            {
                throw new Exception( $"Unable to weaken the AQN: {assemblyQualifiedName}" );
            }
            _deserializations.AddOrUpdate( weakName, driver, ( type, already ) => driver );
        }

        /// <summary>
        /// Finds driver for a Type that may be serializable, exportable, deserializable or not.
        /// </summary>
        /// <param name="t">The type to register. Can be null, typeof(ValueType) or typeof(object): null is returned.</param>
        /// <returns>Null if the type is null, typeof(object) or typeof(ValueType), the driver otherwise.</returns>
        public static IUnifiedTypeDriver FindDriver( Type t )
        {
            if( t == null || t == typeof( object ) || t == typeof( ValueType ) ) return null;
            if( _types.TryGetValue( t, out var info ) )
            {
                return info;
            }
            IUnifiedTypeDriver d;
            if( t.IsEnum )
            {
                d = _types.GetOrAdd( t, new EnumTypeUnifiedDriver( t ) );
            }
            else d = _types.GetOrAdd( t, newType => new TypeInfo( t, (TypeInfo)FindDriver( t.BaseType ) ) );
            if( d.DeserializationDriver != null )
            {
                RegisterDeserializer( t.AssemblyQualifiedName, d.DeserializationDriver );
            }
            return d;
        }

        /// <summary>
        /// Tries to find a desrialization driver for the assembly qualified name or, as a last resort,
        /// from a Type that may be resolved locally and for which a driver can be built automatically.
        /// Returns null if not found.
        /// </summary>
        /// <param name="assemblyQualifiedName">Name to resolve.</param>
        /// <param name="lastResort">
        /// Optional function that may provide a locally available Type.
        /// If this function resturns null, the returned desrialization driver will be null.
        /// </param>
        /// <returns>Null or the deserialization driver to use.</returns>
        public static IDeserializationDriver FindDeserializationDriver( string assemblyQualifiedName, Func<Type> lastResort = null )
        {
            if( !SimpleTypeFinder.WeakenAssemblyQualifiedName( assemblyQualifiedName, out string weakName ) )
            {
                throw new Exception( $"Unable to weaken the AQN: {assemblyQualifiedName}" );
            }
            var d = _deserializations.GetValueWithDefault( weakName, null );
            if( d == null && lastResort != null )
            {
                Type t = lastResort();
                if( t != null )
                {
                    d = FindDriver( t )?.DeserializationDriver;
                }
            }
            return d;
        }

        static void GetAndCheckTypeSerializableParts(
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

            if( ctor != null || v != null || write != null )
            {
                if( ctor == null ) throw new InvalidOperationException( $"Missing public or protected {t.Name}({nameof( BinaryDeserializer )} ) constructor." );
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
