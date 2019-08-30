using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CK.Observable
{
    public class DeserializerRegistry : IDeserializerResolver
    {
        readonly ConcurrentDictionary<string, IDeserializationDriver> _drivers;

        public static readonly DeserializerRegistry Default = new DeserializerRegistry();

        public DeserializerRegistry()
        {
            _drivers = new ConcurrentDictionary<string, IDeserializationDriver>();
            Register( BasicTypeDrivers.DObject.Default.DeserializationDriver, BasicTypeDrivers.DObject.Alias );
            Register( BasicTypeDrivers.DBool.Default.DeserializationDriver, BasicTypeDrivers.DBool.Alias );
            Register( BasicTypeDrivers.DByte.Default.DeserializationDriver, BasicTypeDrivers.DByte.Alias );
            Register( BasicTypeDrivers.DChar.Default.DeserializationDriver, BasicTypeDrivers.DChar.Alias );
            Register( BasicTypeDrivers.DDateTime.Default.DeserializationDriver, BasicTypeDrivers.DDateTime.Alias );
            Register( BasicTypeDrivers.DDateTimeOffset.Default.DeserializationDriver, BasicTypeDrivers.DDateTimeOffset.Alias );
            Register( BasicTypeDrivers.DDecimal.Default.DeserializationDriver, BasicTypeDrivers.DDecimal.Alias );
            Register( BasicTypeDrivers.DDouble.Default.DeserializationDriver, BasicTypeDrivers.DDouble.Alias );
            Register( BasicTypeDrivers.DGuid.Default.DeserializationDriver, BasicTypeDrivers.DGuid.Alias );
            Register( BasicTypeDrivers.DInt16.Default.DeserializationDriver, BasicTypeDrivers.DInt16.Alias );
            Register( BasicTypeDrivers.DInt32.Default.DeserializationDriver, BasicTypeDrivers.DInt32.Alias );
            Register( BasicTypeDrivers.DInt64.Default.DeserializationDriver, BasicTypeDrivers.DInt64.Alias );
            Register( BasicTypeDrivers.DSByte.Default.DeserializationDriver, BasicTypeDrivers.DSByte.Alias );
            Register( BasicTypeDrivers.DSingle.Default.DeserializationDriver, BasicTypeDrivers.DSingle.Alias );
            Register( BasicTypeDrivers.DString.Default.DeserializationDriver, BasicTypeDrivers.DString.Alias );
            Register( BasicTypeDrivers.DTimeSpan.Default.DeserializationDriver, BasicTypeDrivers.DTimeSpan.Alias );
            Register( BasicTypeDrivers.DUInt16.Default.DeserializationDriver, BasicTypeDrivers.DUInt16.Alias );
            Register( BasicTypeDrivers.DUInt32.Default.DeserializationDriver, BasicTypeDrivers.DUInt32.Alias );
            Register( BasicTypeDrivers.DUInt64.Default.DeserializationDriver, BasicTypeDrivers.DUInt64.Alias );
        }

        /// <summary>
        /// Registers a name mapping to a desererializer.
        /// This replaces any existing desererializer associated to this name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="driver">The driver to register.</param>
        public void Register( string name, IDeserializationDriver driver )
        {
            if( String.IsNullOrWhiteSpace( name ) ) throw new ArgumentNullException( nameof( name ) );
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            _drivers.AddOrUpdate( name, driver, ( type, already ) => driver );
        }

        /// <summary>
        /// Registers a desererializer. Its <see cref="IDeserializationDriver.AssemblyQualifiedName"/> is used.
        /// This replaces any existing desererializer associated to this assembly qualified name.
        /// </summary>
        /// <param name="driver">The driver to register.</param>
        /// <param name="alias">An optional alias for the driver.</param>
        public void Register( IDeserializationDriver driver, string alias = null )
        {
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            Register( driver.AssemblyQualifiedName, driver );
            if( alias != null ) Register( alias, driver );
            if( SimpleTypeFinder.WeakenAssemblyQualifiedName( driver.AssemblyQualifiedName, out string weakName )
                && weakName != driver.AssemblyQualifiedName )
            {
                Register( weakName, driver );
            }
        }

        /// <summary>
        /// Tries to find a deserialization driver for a local type.
        /// Returns null if not found.
        /// </summary>
        /// <typeparam name="T">Type for which a deserialization driver must be found.</typeparam>
        /// <returns>Null or the deserialization driver to use.</returns>
        public IDeserializationDriver<T> FindDriver<T>() => (IDeserializationDriver<T>)FindDriver( typeof( T ) );

        /// <summary>
        /// Tries to find a deserialization driver for a local type.
        /// Returns null if not found.
        /// </summary>
        /// <param name="t">Type for which a deserialization driver must be found.</param>
        /// <returns>Null or the deserialization driver to use.</returns>
        public IDeserializationDriver FindDriver( Type t )
        {
            return FindDriver( t.AssemblyQualifiedName, () => t );
        }

        /// <summary>
        /// Tries to find a deserialization driver for a type name or, as a last resort,
        /// from a Type that may be resolved locally and for which a driver can be built automatically.
        /// Returns null if not found.
        /// </summary>
        /// <param name="name">Name to resolve.</param>
        /// <param name="lastResort">
        /// Optional function that may provide a locally available Type.
        /// If this function resturns null, the returned deserialization driver will be null.
        /// </param>
        /// <returns>Null or the deserialization driver to use.</returns>
        public IDeserializationDriver FindDriver( string name, Func<Type> lastResort = null )
        {
            if( _drivers.TryGetValue( name, out var d ) )
            {
                return d;
            }
            if( SimpleTypeFinder.WeakenAssemblyQualifiedName( name, out string weakName )
                && weakName != name
                && _drivers.TryGetValue( name, out d ) )
            {
                return d;
            }
            Type t = lastResort?.Invoke();
            return t == null
                    ? null
                    : _drivers.GetOrAdd( name, n => TryAutoCreate( t ) );
        }

        IDeserializationDriver TryAutoCreate( Type type )
        {
            if( type.IsEnum )
            {
                var underlyingType = Enum.GetUnderlyingType( type );
                var enumType = typeof( EnumTypeDeserializer<,> ).MakeGenericType( type, underlyingType );
                var underlyingDriver = FindDriver( underlyingType );
                return (IDeserializationDriver)Activator.CreateInstance( enumType, underlyingDriver );
            }
            if( type.IsArray )
            {
                var eType = type.GetElementType();
                var eDriver = FindDriver( eType );
                var arrayType = typeof( ArrayDeserializer<> ).MakeGenericType( eType );
                return (IDeserializationDriver)Activator.CreateInstance( arrayType, eDriver );
            }
            var d = AutoTypeRegistry.FindDriver( type ).DeserializationDriver;
            if( d != null ) return d;

            if( type.IsGenericType )
            {
                if( type.GetGenericTypeDefinition() == typeof( List<> ) )
                {
                    var eType = type.GetGenericArguments()[0];
                    var eDriver = FindDriver( eType );
                    var listType = typeof( ListDeserializer<> ).MakeGenericType( eType );
                    d = (IDeserializationDriver)Activator.CreateInstance( listType, eDriver );
                }
                else if( type.GetGenericTypeDefinition() == typeof( Dictionary<,> ) )
                {
                    var eKeyType = type.GetGenericArguments()[0];
                    var eKeyDriver = FindDriver( eKeyType );
                    var eValType = type.GetGenericArguments()[1];
                    var eValDriver = FindDriver( eValType );
                    var dType = typeof( DictionaryDeserializer<,> ).MakeGenericType( eKeyType, eValType );
                    d = (IDeserializationDriver)Activator.CreateInstance( dType, eKeyDriver, eValDriver );
                }
            }
            return d;
        }

    }
}
