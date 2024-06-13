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
        static readonly Type[] _exportParameters = new Type[] { typeof( int ), typeof( ObjectExporter ) };
        static readonly Type[] _exportBaseParameters = new Type[] { typeof( int ), typeof( ObjectExporter ), typeof( IReadOnlyList<ExportableProperty> ) };

        static AutoTypeRegistry()
        {
            _drivers = new ConcurrentDictionary<Type, IUnifiedTypeDriver>();
        }

        /// <summary>
        /// Type based unified driver.
        /// </summary>
        class AutoTypeDriver : IUnifiedTypeDriver, IObjectExportTypeDriver
        {
            readonly Type _type;
            readonly AutoTypeDriver[] _typePath;

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

            public IObjectExportTypeDriver? ExportDriver => _exportError != null ? this : null;

            IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => _exportableProperties;

            bool IObjectExportTypeDriver.IsDefaultBehavior => (_exportError != null && _exportError.Length == 0)
                                                                && _exporter == null
                                                                && _exporterBase == null;

            Type IObjectExportTypeDriver.BaseType => Type;

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
                GetAndCheckTypeAutoParts( t, out _exporter, out _exporterBase );
                _type = t;
                if( baseType != null )
                {
                    var p = new AutoTypeDriver[baseType._typePath.Length + 1];
                    Array.Copy( baseType._typePath, p, baseType._typePath.Length );
                    p[baseType._typePath.Length] = this;
                    _typePath = p;
                }
                else
                {
                    _typePath = new AutoTypeDriver[]{ this };
                }
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
                else
                {
                    // The type is [NotExportable]: it MUST NOT be an ObservableObject.
                    if( typeof(ObservableObject).IsAssignableFrom( Type ) )
                    {
                        throw new CKException( $"Type '{Type:N}' is an ObservableObject: it cannot be [NotExportable]." );
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

        class AutoTypeDriver<T> : AutoTypeDriver, IObjectExportTypeDriver<T>
        {
            public AutoTypeDriver( AutoTypeDriver baseType )
                : base( typeof(T), baseType )
            {
            }

            void IObjectExportTypeDriver<T>.Export( T o, int num, ObjectExporter exporter )
            {
                DoExport( o, num, exporter );
            }

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
        public static IUnifiedTypeDriver? FindDriver( Type t )
        {
            if( t == null ) return null;
            if( _drivers.TryGetValue( t, out var info ) )
            {
                return info;
            }
            var dType = typeof( AutoTypeDriver<> ).MakeGenericType( t );
            var baseTypeInfo = t.BaseType == typeof( object ) || t.BaseType == typeof( ValueType )
                                ? null
                                : (AutoTypeDriver?)FindDriver( t.BaseType );
            return _drivers.GetOrAdd( t, newType => (IUnifiedTypeDriver)Activator.CreateInstance( dType, baseTypeInfo )! );
        }

        static void GetAndCheckTypeAutoParts( Type t,
                                              out MethodInfo? exporter,
                                              out MethodInfo? exporterBase )
        {
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
                Throw.InvalidOperationException( $"Ambiguous methods 'void {t.Name}.Export()'. Choose among the two the Export method that must be used." );
            }
        }

    }
}
