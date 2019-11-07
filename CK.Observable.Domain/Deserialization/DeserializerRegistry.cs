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
            Register( BasicTypeDrivers.DObject.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DType.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DBool.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DByte.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DChar.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DDateTime.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DDateTimeOffset.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DDecimal.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DDouble.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DGuid.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DInt16.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DInt32.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DInt64.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DSByte.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DSingle.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DString.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DTimeSpan.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DUInt16.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DUInt32.Default.DeserializationDriver );
            Register( BasicTypeDrivers.DUInt64.Default.DeserializationDriver );
        }

        /// <summary>
        /// Registers a name mapping to a desererializer.
        /// This replaces any existing desererializer associated to this name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="driver">The driver to register.</param>
        public void Register( string name, IDeserializationDriver driver )
        {
            if( name == null ) throw new ArgumentNullException( nameof( name ) );
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            _drivers.AddOrUpdate( name, driver, ( type, already ) => driver );
        }

        /// <summary>
        /// Registers a desererializer. Its <see cref="IDeserializationDriver.AssemblyQualifiedName"/> is used as well
        /// as the result of the <see cref="SimpleTypeFinder.WeakenAssemblyQualifiedName(string, out string)"/>.
        /// This replaces any existing desererializer associated to these names.
        /// </summary>
        /// <param name="driver">The driver to register.</param>
        public void Register( IDeserializationDriver driver )
        {
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            Register( driver.AssemblyQualifiedName, driver );
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
