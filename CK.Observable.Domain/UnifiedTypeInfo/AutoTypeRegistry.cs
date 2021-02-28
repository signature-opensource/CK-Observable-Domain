using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Transactions;

namespace CK.Observable
{
    /// <summary>
    /// Maintains a set of automatically created internal object that implement <see cref="IUnifiedTypeDriver"/>.
    /// </summary>
    public static class AutoTypeRegistry
    {
        static readonly ConcurrentDictionary<Type, IUnifiedTypeDriver> _drivers;
        static readonly Type[] _ctorParametersNEWSER = new Type[] { typeof( IBinaryDeserializer ), typeof( TypeReadInfo ) };
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
        class AutoTypeDriver : IUnifiedTypeDriver, ITypeSerializationDriver, IDeserializationDeferredDriver, IObjectExportTypeDriver
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

            /// <summary>
            /// This is null if this Type is NOT exportable but this doesn't trigger an error [NotExportable]: ExportDriver is simply null.
            /// <para>
            /// When _exportError is not null, then ExportDriver is not null:
            ///  - This is empty if this Type is exportable: ExportDriver works as expected.
            ///  - This is an error message if any export attempt should trigger an error [NotExportable( Error = "..." )]: ExportDriver throws the message.
            /// </para>
            /// </summary>
            readonly string? _exportError;
            readonly IReadOnlyList<PropertyInfo> _exportableProperties;

            /// <summary>
            /// Gets the type itself.
            /// </summary>
            public Type Type => _type;

            bool ITypeSerializationDriver.IsFinalType => _type.IsValueType;

            public ITypeSerializationDriver? SerializationDriver => _version >= 0 ? this : null;

            public IDeserializationDriver? DeserializationDriver => _version >= 0 ? this : null;

            public IObjectExportTypeDriver? ExportDriver => _exportError != null ? this : null;

            IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => _exportableProperties;

            bool IObjectExportTypeDriver.IsDefaultBehavior => (_exportError != null && _exportError.Length == 0)
                                                                && _exporter == null
                                                                && _exporterBase == null;

            Type IObjectExportTypeDriver.BaseType => Type;

            bool ITypeSerializationDriver.AllowDeferred => true;

            /// <summary>
            /// Invokes the deserialization constructor.
            /// </summary>
            /// <param name="r">The deserializer.</param>
            /// <param name="readInfo">
            /// The type based information (with versions and ancestors) of the type as it has been written.
            /// Null if the type has been previously written by an external driver.
            /// </param>
            /// <returns>The new instance.</returns>
            void IDeserializationDeferredDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo? readInfo, object o )
            {
                DoReadInstance( r, readInfo, o );
            }
            object IDeserializationDeferredDriver.Allocate( IBinaryDeserializer r, TypeReadInfo readInfo )
            {
                return r.ImplementationServices.CreateUninitializedInstance( Type, readInfo.IsTrackedObject );
            }

            object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo? readInfo )
            {
                return DoReadInstance( r, readInfo );
            }

            string InvokeCtor( object o, object[] callParams, TypeReadInfo readInfo )
            {
                if( _baseType != null )
                {
                    if( readInfo.BaseType == null ) 
                    {
                        return $"Missing base type info for type {readInfo.SimpleTypeName}. The type hierarchy has changed between serialization and deserialization.";
                    }
                    var error = _baseType.InvokeCtor( o, callParams, readInfo.BaseType );
                    if( error != null ) return error;
                    if( o is IDestroyable d && d.IsDestroyed ) return null;
                }
                else if( readInfo.TypePath.Count > 1 )
                {
                    return $"Missing base type constructor {readInfo.SimpleTypeName}( IBinaryDeserializer, TypeReadInfo?).";
                }
                callParams[1] = readInfo;
                _ctor?.Invoke( o, callParams );
                return null;
            }

            protected object DoReadInstance( IBinaryDeserializer r, TypeReadInfo? readInfo )
            {
                var o = r.ImplementationServices.CreateUninitializedInstance( Type, readInfo?.IsTrackedObject ?? false );
                return DoReadInstance( r, readInfo, o );
            }

            object DoReadInstance( IBinaryDeserializer r, TypeReadInfo? readInfo, object o )
            {
                try
                {
                    var callParams = new object[] { r, null };
                    if( readInfo == null )
                    {
                        _ctor?.Invoke( o, callParams );
                    }
                    else
                    {
                        var error = InvokeCtor( o, callParams, readInfo );
                        if( error != null ) throw new InvalidDataException( error );
                    }
                }
                catch( TargetInvocationException ex )
                {
                    var inner = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture( ex.InnerException );
                    inner.Throw();
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

            protected void DoWriteData( BinarySerializer w, object o )
            {
                var parameters = new object[] { w };
                if( o is IDestroyable d && d.IsDestroyed )
                {
                    _typePath[0]._writer?.Invoke( o, w );
                    w.DisposedTracker?.Invoke( d );
                }
                else
                {
                    foreach( var t in _typePath )
                    {
                        t._writer?.Invoke( o, w );
                    }
                }
            }

            void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter )
            {
                DoExport( o, num, exporter );
            }

            protected void DoExport( object o, int num, ObjectExporter exporter )
            {
                Debug.Assert( _exportError != null, "When null, this is not exportable." );
                if( _exportError.Length > 0 )
                {
                    throw new CKException( _exportError );
                }
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

                // CS0192  A readonly field cannot be used as a ref or out value (except in a constructor).
                // Using exportError to set _exportError.
                // Defaults to null: not exportable, no error.
                string? exportError = null;

                if( CheckTypeOrPropertyNotExportable( Type, ref exportError ) )
                {
                    // Should be exportable unless a property prevents it.
                    exportError = String.Empty;
                    if( _exporter == null )
                    {
                        // This is far from perfect: the property type may be not exportable for another
                        // reason than being marked with the NotExportableAttribute... However, this would made
                        // this property list dependent on the IExporterResolver being used.
                        // That would imply a heavy refactoring of the export API. For the moment, this does the job.
                        _exportableProperties = Type.GetProperties().Where( p => p.GetIndexParameters().Length == 0
                                                            && CheckTypeOrPropertyNotExportable( p, ref exportError )
                                                            && CheckTypeOrPropertyNotExportable( p.PropertyType, ref exportError ) )
                                                .ToArray();
                    }
                }
                _exportError = exportError;
            }

            static bool CheckTypeOrPropertyNotExportable( MemberInfo m, ref string? exportError )
            {
                bool exportable = true;
                foreach( var a in m.GetCustomAttributes<NotExportableAttribute>() )
                {
                    if( !String.IsNullOrWhiteSpace( a.Error ) )
                    {
                        var n = m.DeclaringType?.Name;
                        if( n != null ) n += '.' + m.Name;
                        else n = m.Name;
                        if( !String.IsNullOrEmpty( exportError ) ) exportError += Environment.NewLine;
                        exportError += $"Exporting '{n}' is forbidden: {a.Error}";
                    }
                    exportable = false;
                }
                return exportable;
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
                                     _ctorParametersNEWSER,
                                     null );
            if( ctor != null && !ctor.IsPrivate )
            {
                throw new InvalidOperationException( $"Deserialization constructor '{t.Name}( IBinaryDeserializer r, TypeReadInfo? info ) must be private." );
            }

            // void Write( BinarySerializer w ) can be a regular method. We check its 'Private' if we have a Version or a Ctor.
            var writeMethod = t.GetMethod( "Write",
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                 null,
                                 _writeParameters,
                                 null );

            if( v != null || ctor != null )
            {
                if( v == null ) throw new InvalidOperationException( $"Missing [{nameof( SerializationVersionAttribute )}] attribute on type '{t.Name}'." );
                Debug.Assert( v.Value >= 0 );
                version = v.Value;

                if( writeMethod != null && !writeMethod.IsPrivate )
                {
                    throw new InvalidOperationException( $"Serialization method 'void {t.Name}.Write( {nameof( BinarySerializer )} )' must be private." );
                }

                // Either both are defined or none of them.
                if( (writeMethod == null) != (ctor == null) )
                {
                    throw new InvalidOperationException( $"Private 'void {t.Name}.Write( {nameof( BinarySerializer )} )' and constructor '{t.Name}( IBinaryDeserializer r, TypeReadInfo? info )' must be both defined or not at all." );
                }

                if( writeMethod != null )
                {
                    // Creates the writer compiled method.
                    var pTarget = ParameterExpression.Parameter( typeof( object ) );
                    var pCtx = ParameterExpression.Parameter( typeof( BinarySerializer ) );
                    var body = Expression.Call( Expression.Convert( pTarget, t ), writeMethod, pCtx );
                    writer = Expression.Lambda<Action<object, BinarySerializer>>( body, pTarget, pCtx ).Compile();
                }
                else
                {
                    writer = null;
                }
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
