using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Registers serialization drivers. This can be instanciated if needed but most often,
    /// the <see cref="Default"/> registry is enough.
    /// used 
    /// </summary>
    public class SerializerRegistry : ISerializerResolver
    {
        readonly ConcurrentDictionary<Type, ITypeSerializationDriver> _types;

        /// <summary>
        /// Gets the default, shared, registry.
        /// </summary>
        public static readonly SerializerRegistry Default = new SerializerRegistry();

        /// <summary>
        /// Initializes a new <see cref="SerializerRegistry"/> with preregistered
        /// basic serialization drivers.
        /// </summary>
        public SerializerRegistry()
        {
            _types = new ConcurrentDictionary<Type, ITypeSerializationDriver>();
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

            void Reg<T>( IUnifiedTypeDriver<T> u ) => Register( u.Type, u.SerializationDriver );
        }

        /// <summary>
        /// Registers a driver.
        /// This replaces any existing export driver for the Type.
        /// </summary>
        /// <param name="driver">The driver to register.</param>
        public void Register( Type t, ITypeSerializationDriver driver )
        {
            _types.AddOrUpdate( t, driver, ( type, already ) => driver );
        }

        /// <summary>
        /// Finds a serialization driver for a Type.
        /// </summary>
        /// <typeparam name="T">The type for which a driver must be found.</typeparam>
        /// <returns>Null if the type is null, the driver otherwise.</returns>
        public ITypeSerializationDriver<T> FindDriver<T>() => (ITypeSerializationDriver<T>)FindDriver( typeof( T ) );

        /// <summary>
        /// Finds a serialization driver for a Type.
        /// </summary>
        /// <param name="t">The type for which a driver must be found. Can be null: null is returned.</param>
        /// <returns>Null if the type is null, the driver otherwise.</returns>
        public ITypeSerializationDriver FindDriver( Type t )
        {
            if( t == null ) return null;
            if( t == typeof( object ) || t == typeof( ValueType ) ) return BasicTypeDrivers.DObject.Default;
            return _types.GetOrAdd( t, type => TryAutoCreate( type ) );
        }

        ITypeSerializationDriver TryAutoCreate( Type type )
        {
            if( type.IsEnum )
            {
                var underlyingType = Enum.GetUnderlyingType( type );
                var enumType = typeof( EnumTypeSerializer<,> ).MakeGenericType( type, underlyingType );
                var underlyingDriver = FindDriver( underlyingType );
                return (ITypeSerializationDriver)Activator.CreateInstance( enumType, underlyingDriver );
            }
            if( type.IsArray )
            {
                var eType = type.GetElementType();
                var eDriver = FindDriver( eType );
                var arrayType = typeof( ArraySerializer<> ).MakeGenericType( eType );
                return (ITypeSerializationDriver)Activator.CreateInstance( arrayType, eDriver );
            }
            var d = AutoTypeRegistry.FindDriver( type ).SerializationDriver;
            if( d != null ) return d;

            if( type.IsGenericType )
            {
                if( type.GetGenericTypeDefinition() == typeof( List<> ) )
                {
                    var eType = type.GetGenericArguments()[0];
                    var eDriver = FindDriver( eType );
                    var listType = typeof( ListTypeSerializer<> ).MakeGenericType( eType );
                    d = (ITypeSerializationDriver)Activator.CreateInstance( listType, eDriver );
                }
                else if( type.GetGenericTypeDefinition() == typeof( Dictionary<,> ) )
                {
                    var eKeyType = type.GetGenericArguments()[0];
                    var eKeyDriver = FindDriver( eKeyType );
                    var eValType = type.GetGenericArguments()[1];
                    var eValDriver = FindDriver( eValType );
                    var dType = typeof( DictionarySerializer<,> ).MakeGenericType( eKeyType, eValType );
                    d = (ITypeSerializationDriver)Activator.CreateInstance( dType, eKeyDriver, eValDriver );
                }
            }
            return d;
        }
    }
}
