using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable;

/// <summary>
/// Registers export drivers. This can be instantiated if needed but most often,
/// the <see cref="Default"/> registry is enough.
/// </summary>
public class ExporterRegistry : IExporterResolver
{
    readonly ConcurrentDictionary<Type, IObjectExportTypeDriver> _drivers;

    /// <summary>
    /// Gets the default, shared, registry.
    /// </summary>
    public static readonly ExporterRegistry Default = new ExporterRegistry();

    /// <summary>
    /// Initializes a new <see cref="ExporterRegistry"/> with preregistered
    /// basic export drivers.
    /// </summary>
    public ExporterRegistry()
    {
        _drivers = new ConcurrentDictionary<Type, IObjectExportTypeDriver>();
        RegisterDriver( BasicTypeDrivers.DObject.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DBool.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DByte.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DChar.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DDateTime.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DDateTimeOffset.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DDecimal.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DDouble.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DGuid.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DInt16.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DInt32.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DInt64.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DSByte.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DSingle.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DString.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DTimeSpan.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DUInt16.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DUInt32.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DUInt64.Default.ExportDriver );
        RegisterDriver( BasicTypeDrivers.DNormalizedPath.Default.ExportDriver );
    }

    /// <summary>
    /// Registers a driver.
    /// This replaces any existing export driver for the Type.
    /// </summary>
    /// <param name="driver">The driver to register.</param>
    public void RegisterDriver( IObjectExportTypeDriver driver )
    {
        _drivers.AddOrUpdate( driver.BaseType, driver, ( type, already ) => driver );
    }

    /// <summary>
    /// Finds an export driver for a Type or null if the type is not exportable.
    /// </summary>
    /// <typeparam name="T">The type for which a driver must be found.</typeparam>
    /// <returns>The driver or null if the type is not exportable.</returns>
    public IObjectExportTypeDriver<T> FindDriver<T>() => (IObjectExportTypeDriver<T>)FindDriver( typeof( T ) );

    /// <summary>
    /// Finds an export driver for a Type or null if the type is not exportable.
    /// </summary>
    /// <param name="t">The type for which a driver must be found. Can be null: null is returned.</param>
    /// <returns>The driver or null if the type is not exportable.</returns>
    public IObjectExportTypeDriver FindDriver( Type t )
    {
        if( t == null ) return null;
        if( t == typeof( object ) || t == typeof( ValueType ) ) return BasicTypeDrivers.DObject.Default;
        return _drivers.GetOrAdd( t, type => TryAutoCreate( type ) );
    }

    IObjectExportTypeDriver TryAutoCreate( Type type )
    {
        if( type.IsEnum )
        {
            var underlyingType = Enum.GetUnderlyingType( type );
            var enumType = typeof( EnumTypeExporter<,> ).MakeGenericType( type, underlyingType );
            var underlyingDriver = FindDriver( underlyingType );
            return (IObjectExportTypeDriver)Activator.CreateInstance( enumType, underlyingDriver )!;
        }
        if( type.IsArray )
        {
            var eType = type.GetElementType()!;
            var eDriver = FindDriver( eType );
            var arrayType = typeof( EnumerableTypeExporter<> ).MakeGenericType( eType );
            return (IObjectExportTypeDriver)Activator.CreateInstance( arrayType, eDriver )!;
        }
        var d = AutoTypeRegistry.FindDriver( type ).ExportDriver;
        if( d == null || d.IsDefaultBehavior )
        {
            var enumerableDriver = TryCreateEnumerableExportDriver( type );
            if( enumerableDriver != null ) d = enumerableDriver;
        }
        return d;
    }

    IObjectExportTypeDriver TryCreateEnumerableExportDriver( Type type )
    {
        IObjectExportTypeDriver d = null;
        if( typeof( System.Collections.IEnumerable ).IsAssignableFrom( type ) )
        {
            var enumType = type.GetInterfaces().FirstOrDefault( t => t.IsGenericType
                                                      && t.GetGenericTypeDefinition() == typeof( IEnumerable<> ) );
            if( enumType != null )
            {
                var itemType = enumType.GetGenericArguments()[0];
                if( itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof( KeyValuePair<,> ) )
                {
                    var kvTypes = itemType.GetGenericArguments();
                    var mapType = typeof( MapTypeExportDriver<,> ).MakeGenericType( kvTypes[0], kvTypes[1] );
                    var kTypeExporter = FindDriver( kvTypes[0] );
                    var vTypeExporter = FindDriver( kvTypes[1] );
                    if( kTypeExporter != null && vTypeExporter != null )
                    {
                        d = (IObjectExportTypeDriver)Activator.CreateInstance( mapType, kTypeExporter, vTypeExporter );
                    }
                }
                else
                {
                    var itemExporter = FindDriver( itemType );
                    if( itemExporter != null )
                    {
                        var arrayType = typeof( EnumerableTypeExporter<> ).MakeGenericType( itemType );
                        d = (IObjectExportTypeDriver)Activator.CreateInstance( arrayType, itemExporter );
                    }
                }
            }
        }
        return d;
    }
}
