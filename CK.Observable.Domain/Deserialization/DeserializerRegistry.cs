using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;

namespace CK.Observable
{
    /// <summary>
    /// Registers deserialization drivers. This can be instantiated if needed but most often,
    /// the <see cref="Default"/> registry is enough.
    /// </summary>
    public class DeserializerRegistry : IDeserializerResolver
    {
        readonly ConcurrentDictionary<string, IDeserializationDriver> _drivers;

        /// <summary>
        /// Gets the default, shared, registry.
        /// </summary>
        public static readonly DeserializerRegistry Default = new DeserializerRegistry();

        /// <summary>
        /// Initializes a new <see cref="DeserializerRegistry"/> with preregistered
        /// basic deserialization drivers.
        /// </summary>
        public DeserializerRegistry()
        {
            _drivers = new ConcurrentDictionary<string, IDeserializationDriver>();
            Reg( BasicTypeDrivers.DObject.Default );
            Reg( BasicTypeDrivers.DType.Default );
            Reg( BasicTypeDrivers.DBool.Default );
            Reg( BasicTypeDrivers.DByte.Default );
            Reg( BasicTypeDrivers.DChar.Default );
            Reg( BasicTypeDrivers.DDateTime.Default );
            Reg( BasicTypeDrivers.DDateTimeOffset.Default );
            Reg( BasicTypeDrivers.DDecimal.Default );
            Reg( BasicTypeDrivers.DDouble.Default );
            Reg( BasicTypeDrivers.DGuid.Default );
            Reg( BasicTypeDrivers.DInt16.Default );
            Reg( BasicTypeDrivers.DInt32.Default );
            Reg( BasicTypeDrivers.DInt64.Default );
            Reg( BasicTypeDrivers.DSByte.Default );
            Reg( BasicTypeDrivers.DSingle.Default );
            Reg( BasicTypeDrivers.DString.Default );
            Reg( BasicTypeDrivers.DTimeSpan.Default );
            Reg( BasicTypeDrivers.DUInt16.Default );
            Reg( BasicTypeDrivers.DUInt32.Default );
            Reg( BasicTypeDrivers.DUInt64.Default );

            void Reg<T>( IUnifiedTypeDriver<T> u ) => Register( u.Type, u.DeserializationDriver );
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
        /// Registers a desererializer. The <see cref="Type.AssemblyQualifiedName"/> is used as well
        /// as the result of the <see cref="SimpleTypeFinder.WeakenAssemblyQualifiedName(string, out string)"/>.
        /// This replaces any existing desererializer associated to these names.
        /// </summary>
        /// <param name="type">The type for which names must be registered.</param>
        /// <param name="driver">The driver to register.</param>
        public void Register( Type type, IDeserializationDriver driver )
        {
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            var n = type.AssemblyQualifiedName;
            Register( n, driver );
            if( SimpleTypeFinder.WeakenAssemblyQualifiedName( n, out string weakName ) && weakName != n )
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
        public IDeserializationDriver<T>? FindDriver<T>() => (IDeserializationDriver<T>)FindDriver( typeof( T ) );

        /// <summary>
        /// Tries to find a deserialization driver for a local type.
        /// Returns null if not found.
        /// </summary>
        /// <param name="t">Type for which a deserialization driver must be found.</param>
        /// <returns>Null or the deserialization driver to use.</returns>
        public IDeserializationDriver? FindDriver( Type t )
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
        /// If this function returns null, the returned deserialization driver will be null.
        /// </param>
        /// <returns>Null or the deserialization driver to use.</returns>
        public IDeserializationDriver? FindDriver( string name, Func<Type>? lastResort = null )
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
            Type? t = lastResort?.Invoke();
            return t == null
                    ? null
                    : _drivers.GetOrAdd( name, n => TryAutoCreate( t )! );
        }

        IDeserializationDriver? TryAutoCreate( Type type )
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
