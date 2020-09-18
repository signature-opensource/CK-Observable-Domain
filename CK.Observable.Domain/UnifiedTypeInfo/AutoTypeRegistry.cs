using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CK.Observable
{
    /// <summary>
    /// Maintains a set of automatically created internal object that implement <see cref="IUnifiedTypeDriver"/>.
    /// </summary>
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
            readonly AutoTypeDriver? _baseType;
            readonly AutoTypeDriver[] _typePath;

            // Serialization.
            readonly Action<object,BinarySerializer> _writer;

            /// <summary>
            /// The version from the <see cref="SerializationVersionAttribute"/>.
            /// </summary>
            readonly int _version;

            // Deserialization.
            readonly ConstructorInfo _ctor;

            // Export.
            readonly MethodInfo? _exporter;
            readonly MethodInfo? _exporterBase;
            readonly IReadOnlyList<PropertyInfo> _exportableProperties;
            readonly bool _isExportable;

            /// <summary>
            /// Gets the type itself.
            /// </summary>
            public Type Type => _type;

            bool ITypeSerializationDriver.IsFinalType => _type.IsSealed || _type.IsValueType;

            public ITypeSerializationDriver? SerializationDriver => _writer != null ? this : null;

            public IDeserializationDriver? DeserializationDriver => _ctor != null ? this : null;

            public IObjectExportTypeDriver? ExportDriver => _isExportable ? this : null;

            IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => _exportableProperties;

            bool IObjectExportTypeDriver.IsDefaultBehavior => _isExportable && _exporter == null && _exporterBase == null;

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
            object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo? readInfo ) 
            {
                return DoReadInstance( r, readInfo );
            }

            protected object DoReadInstance( IBinaryDeserializer r, TypeReadInfo? readInfo )
            {
                var ctx = r.ImplementationServices.PushConstructorContext( readInfo );

                var o = r.ImplementationServices.CreateUninitializedInstance( Type, readInfo?.IsTrackedObject ?? false );

                // Must be replaced with an emitted DynamicMethod or compiled expression.
                // Note: a simple Expression.New cannot be used since the UnitializedObject MUST be registered (when IsTrackedObject)
                //       before the ctor code is executed (to handle cycles).
                //       To allow Expression.New to be used, it must be the StartReading of level 0 (root) that does the tracking.
                //       This has to be investigated.
                try
                {
                    _ctor.Invoke( o, new object[] { ctx } );
                }
                catch( TargetInvocationException ex )
                {
                    var inner = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture( ex.InnerException );
                    inner.Throw();
                }
                r.ImplementationServices.PopConstructorContext();
                if( r.IsDebugMode && readInfo != null )
                {
                    r.ReadString( "After: " + readInfo.DescribeAutoTypePathItem( readInfo ) );
                }
                return o;
            }

            /// <summary>
            /// Writes types from this <see cref="Type"/> to the object base type
            /// that have not been written yet (they are still unknown to the Serializer).
            /// </summary>
            /// <param name="s">The serializer.</param>
            void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s )
            {
                AutoTypeDriver? tInfo = this;
                while( s.DoWriteSimpleType( tInfo?.Type ) )
                {
                    Debug.Assert( tInfo != null && tInfo._version >= 0 );
                    s.WriteSmallInt32( tInfo!._version );
                    tInfo = tInfo._baseType;
                }
            }

            /// <summary>
            /// Calls the Write methods from base object type down to this one.
            /// </summary>
            /// <param name="w">The serializer.</param>
            /// <param name="o">The object instance.</param>
            void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => DoWriteData( w, o );

            string SimpleTypeName => _type.AssemblyQualifiedName.Split( ',' )[0];

            string DescribeAutoTypePathItem( AutoTypeDriver infoInPath )
            {
                Debug.Assert( Array.IndexOf( _typePath, infoInPath ) >= 0 );
                bool isRoot = _baseType == null;
                bool isLeaf = this == infoInPath;
                var msg = isRoot
                            ? (
                                isLeaf ? " (root and final)" : $" (root type of {SimpleTypeName})"
                              )
                            : (
                                isLeaf ? " (final type)" : $" (base type of {SimpleTypeName})"
                              );
                msg = infoInPath.SimpleTypeName + msg;
                return msg;
            }

            protected void DoWriteData( BinarySerializer w, object o )
            {
                var parameters = new object[] { w };
                foreach( var t in _typePath )
                {
                    t._writer( o, w );
                    if( w.IsDebugMode )
                    {
                        w.Write( "After: " + DescribeAutoTypePathItem( t ) );
                    }
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
                GetAndCheckTypeAutoParts( t, out _version, out _ctor, out _writer, out _exporter, out _exporterBase );
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

                // Propagates exporterBase method if any regardless of exportability
                // of this exact type.
                if( _exporterBase == null )
                {
                    _exporterBase = baseType?._exporterBase;
                }
                _exportableProperties = Array.Empty<PropertyInfo>();
                if( !Type.GetCustomAttributes<NotExportableAttribute>().Any() )
                {
                    _isExportable = true;
                    if( _exporter == null )
                    {
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
            out Action<object,BinarySerializer> writer,
            out MethodInfo exporter,
            out MethodInfo exporterBase )
        {
            int? v = t.GetCustomAttribute<SerializationVersionAttribute>()?.Version;
            ctor = t.GetConstructor( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                     null,
                                     _ctorParameters,
                                     null );

            var writeMethod = t.GetMethod( "Write",
                                 BindingFlags.Instance | BindingFlags.NonPublic,
                                 null,
                                 _writeParameters,
                                 null );

            if( v != null || writeMethod != null )
            {
                if( v == null ) throw new InvalidOperationException( $"Missing [{nameof( SerializationVersionAttribute )}] attribute on type '{t.Name}'." );
                Debug.Assert( v.Value >= 0 );
                if( writeMethod == null ) throw new InvalidOperationException( $"Missing 'private void {t.Name}.Write({nameof( BinarySerializer )} )' method." );
                version = v.Value;

                // Creates the writer compiled method.
                var pTarget = ParameterExpression.Parameter( typeof(object) );
                var pCtx = ParameterExpression.Parameter( typeof( BinarySerializer ) );               
                var body = Expression.Call( Expression.Convert( pTarget, t ), writeMethod, pCtx );
                writer = Expression.Lambda<Action<object,BinarySerializer>>( body, pTarget, pCtx ).Compile();
            }
            else
            {
                version = -1;
                writer = null;
            }

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
