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
    class SerializableTypes
    {
        static readonly ConcurrentDictionary<Type, ITypeSerializationDriver> _types;
        static readonly Type[] _ctorParameters;
        static readonly Type[] _writeParameters;
        static readonly Type[] _exportParameters;

        static SerializableTypes()
        {
            _types = new ConcurrentDictionary<Type, ITypeSerializationDriver>();
            _ctorParameters = new Type[] { typeof( Deserializer ) };
            _writeParameters = new Type[] { typeof( Serializer ) };
            _exportParameters = new Type[] { typeof( object ), typeof( int ), typeof( ObjectExporter ) };
            Register( new BasicTypeDrivers.DBool() );
            Register( new BasicTypeDrivers.DByte() );
            Register( new BasicTypeDrivers.DChar() );
            Register( new BasicTypeDrivers.DDateTime() );
            Register( new BasicTypeDrivers.DDateTimeOffset() );
            Register( new BasicTypeDrivers.DDecimal() );
            Register( new BasicTypeDrivers.DDouble() );
            Register( new BasicTypeDrivers.DGuid() );
            Register( new BasicTypeDrivers.DInt16() );
            Register( new BasicTypeDrivers.DInt32() );
            Register( new BasicTypeDrivers.DInt64() );
            Register( new BasicTypeDrivers.DSByte() );
            Register( new BasicTypeDrivers.DSingle() );
            Register( new BasicTypeDrivers.DString() );
            Register( new BasicTypeDrivers.DTimeSpan() );
            Register( new BasicTypeDrivers.DUInt16() );
            Register( new BasicTypeDrivers.DUInt32() );
            Register( new BasicTypeDrivers.DUInt64() );
        }

        /// <summary>
        /// Serializable <see cref="TypeSerializationKind.TypeBased"/> descriptor.
        /// </summary>
        public class TypeInfo : ITypeSerializationDriver
        {
            /// <summary>
            /// Gets the base type. Null if this type is a root (its base type is Object).
            /// </summary>
            public readonly TypeInfo BaseType;

            /// <summary>
            /// Gets the type itself.
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// Always <see cref="TypeSerializationKind.TypeBased"/>.
            /// </summary>
            public TypeSerializationKind SerializationKind => TypeSerializationKind.TypeBased;

            /// <summary>
            /// The version from the <see cref="SerializationVersionAttributeAttribute"/>.
            /// </summary>
            public readonly int Version;

            /// <summary>
            /// Gets the types from the base types (excluding Object) up to this one.
            /// </summary>
            public IReadOnlyList<TypeInfo> TypePath => _typePath;

            public bool IsExportable { get; }

            readonly TypeInfo[] _typePath;
            readonly MethodInfo _write;
            readonly ConstructorInfo _ctor;

            // Export.
            readonly MethodInfo _exporter;
            readonly IReadOnlyList<PropertyInfo> _exportedProperties;

            /// <summary>
            /// Calls the Write methods on <see cref="TypePath"/>.
            /// </summary>
            /// <param name="w">The serializer.</param>
            /// <param name="o">The object instance.</param>
            public void WriteData( Serializer w, object o )
            {
                var parameters = new object[] { w };
                foreach( var t in _typePath )
                {
                    t._write.Invoke( o, new object[] { w } );
                }
            }

            /// <summary>
            /// Invokes the deserialization constructor.
            /// </summary>
            /// <param name="r">The deserializer.</param>
            /// <param name="readInfo">
            /// The type based information (with versions and ancestors) of the type as it has been written.
            /// Null if the type has been previously written by an external driver.
            /// </param>
            /// <returns>The new instance.</returns>
            public object ReadInstance( Deserializer r, ObjectStreamReader.TypeReadInfo readInfo )
            {
                r.PushCtorContext( readInfo );
                var o = _ctor.Invoke( new object[] { r } );
                r.PopCtorContext();
                return o;
            }

            /// <summary>
            /// Writes types in <see cref="TypePath"/> that has not been
            /// written yet (they are unknown to the Serializer).
            /// </summary>
            /// <param name="s">The serializer.</param>
            public void WriteTypeInformation( Serializer s )
            {
                s.DoWriteSerializableType( this );
            }

            public void Export(object o, int num, ObjectExporter exporter)
            {
                if( _exporter != null )
                {
                    _exporter.Invoke( o, new object[] { num, exporter } );
                }
                else
                {
                    if( _exportedProperties == null ) throw new InvalidOperationException( $"Type {Type.Name} is not exportable." );
                    if( _exportedProperties.Count == 0 )
                    {
                        exporter.Target.EmitEmptyObject( num );
                    }
                    else
                    {
                        exporter.Target.EmitStartObject( num, ObjectExportedKind.Object );
                        foreach( var p in _exportedProperties )
                        {
                            exporter.Target.EmitObjectProperty( p.Name );
                            exporter.ExportObject( p.GetValue( o ) );
                        }
                        exporter.Target.EmitEndObject( num, ObjectExportedKind.Object );
                    }
                }
            }

            internal TypeInfo( TypeInfo baseType, TypeSerializableParts parts )
            {
                Type = parts.Type;
                Version = parts.Version.Value;
                _write = parts.Write;
                _ctor = parts.Ctor;
                BaseType = baseType;
                if( baseType != null )
                {
                    var p = new TypeInfo[baseType._typePath.Length + 1];
                    Array.Copy( baseType._typePath, p, baseType._typePath.Length );
                    p[baseType._typePath.Length] = this;
                    _typePath = p;
                }
                else _typePath = new[] { this };
                _exporter = Type.GetMethod( "Export",
                                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                            null,
                                            _exportParameters,
                                            null );
                if( _exporter != null )
                {
                    IsExportable = true;
                }
                else
                {
                    IsExportable = !Type.GetCustomAttributes<NotExportableAttribute>().Any();
                    if( IsExportable )
                    {
                        _exportedProperties = Type.GetProperties().Where( p => p.GetIndexParameters().Length == 0
                                                            && !p.GetCustomAttributes<NotExportableAttribute>().Any() )
                                             .ToArray();
                    }
                }
            }
        }

        public static void Register( ITypeSerializationDriver driver )
        {
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            if( !_types.TryAdd( driver.Type, driver ) )
            {
                throw new InvalidOperationException( $"Type {driver.Type} is already associated to another driver." );
            }
        }

        /// <summary>
        /// Finds driver for a Type that must be serializable or not.
        /// If the type must be serializable and it is not, an exception is thrown.
        /// Thia always returns null when type is null, typeof(object) or typeof(ValueType).
        /// </summary>
        /// <param name="t">The type to register. Can be null or typeof(object): null is returned.</param>
        /// <param name="requirements">Serializable requirements.</param>
        /// <returns>Null if the type is not a serializable type or is null, typeof(object) or typeof(ValueType), the driver otherwise.</returns>
        public static ITypeSerializationDriver FindDriver( Type t, TypeSerializationKind requirements )
        {
            if( t == null || t == typeof( object ) || t == typeof( ValueType ) ) return null;
            if( _types.TryGetValue( t, out var info ) )
            {
                if( info == null && (requirements & TypeSerializationKind.Serializable) != 0 )
                {
                    GetTypeSerializableParts( t ).CheckValid( requirements );
                }
                if( info != null
                    && (requirements & info.SerializationKind) == 0 )
                {
                    throw new InvalidOperationException( $"Type {t.Name} should support {requirements} serialization but it is {info.SerializationKind}." );
                }
                return info;
            }
            var parts = GetTypeSerializableParts( t );
            if( requirements == TypeSerializationKind.TypeBased ) parts.CheckValid( requirements );
            else if( parts.IsValid ) requirements = TypeSerializationKind.TypeBased;
            else
            {
                return _types.GetOrAdd( t, (TypeInfo)null );
            }
            return _types.GetOrAdd( t, newType => new TypeInfo( (TypeInfo)FindDriver( t.BaseType, requirements ), parts ) );
        }

        internal struct TypeSerializableParts
        {
            public readonly Type Type;
            public readonly int? Version;
            public readonly ConstructorInfo Ctor;
            public readonly MethodInfo Write;

            public TypeSerializableParts( Type t, int? v, ConstructorInfo c, MethodInfo m )
            {
                Type = t;
                Version = v;
                Ctor = c;
                Write = m;
            }

            public void CheckValid( TypeSerializationKind requirements )
            {
                if( requirements == TypeSerializationKind.External )
                {
                    if( IsValid )
                    {
                        throw new InvalidOperationException( $"Type {Type.Name} is TypeBased serializable but an external driver is required." );
                    }
                    throw new InvalidOperationException( $"Type {Type.Name} is not serializable. An external driver is required." );
                }
                if( Ctor == null && Version == null && Write == null )
                {
                    throw new InvalidOperationException( $"Type {Type.Name} is not serializable. Either define [{nameof( SerializationVersionAttribute )}], public or protected {Type.Name}({nameof( Deserializer )} ) constructor and 'void {Type.Name}.Write({nameof( Serializer )} )' method on it or register an external driver." );
                }
                if( Ctor == null ) throw new InvalidOperationException( $"Missing public or protected {Type.Name}({nameof( Deserializer )} ) constructor." );
                if( Version == null ) throw new InvalidOperationException( $"Missing [{nameof(SerializationVersionAttribute)}] attribute on type {Type.Name}." );
                if( Write == null ) throw new InvalidOperationException( $"Missing 'void {Type.Name}.Write({nameof( Serializer )} )' method." );
            }

            public bool IsValid => Version != null && Ctor != null && Write != null;

        }

        static TypeSerializableParts GetTypeSerializableParts( Type t )
        {
            var v = t.GetCustomAttribute<SerializationVersionAttribute>()?.Version;
            var ctor = t.GetConstructor( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                         null,
                                        _ctorParameters,
                                        null );
            var write = t.GetMethod( "Write",
                                      BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                      null,
                                      _writeParameters,
                                      null );
            return new TypeSerializableParts( t, v, ctor, write);
        }

    }
}
