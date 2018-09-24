using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class SerializerRegistry : ISerializerResolver
    {
        readonly ConcurrentDictionary<Type, ITypeSerializationDriver> _types;

        public static readonly SerializerRegistry Default = new SerializerRegistry();

        public SerializerRegistry()
        {
            _types = new ConcurrentDictionary<Type, ITypeSerializationDriver>();
            RegisterDriver( BasicTypeDrivers.DObject.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DBool.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DByte.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DChar.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DDateTime.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DDateTimeOffset.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DDecimal.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DDouble.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DGuid.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DInt16.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DInt32.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DInt64.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DSByte.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DSingle.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DString.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DTimeSpan.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DUInt16.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DUInt32.Default.SerializationDriver );
            RegisterDriver( BasicTypeDrivers.DUInt64.Default.SerializationDriver );
        }

        /// <summary>
        /// Registers a full driver (<see cref="UnifiedTypeDriverExtension.CheckValidFullDriver"/> is called).
        /// This replaces any existing serialization driver for the Type.
        /// </summary>
        /// <param name="driver">The driver to register.</param>
        public void RegisterValidFullDriver( IUnifiedTypeDriver driver )
        {
            driver.CheckValidFullDriver();
            RegisterDriver( driver.SerializationDriver );
        }

        /// <summary>
        /// Registers a driver.
        /// This replaces any existing export driver for the Type.
        /// </summary>
        /// <param name="driver">The driver to register.</param>
        public void RegisterDriver( ITypeSerializationDriver driver )
        {
            _types.AddOrUpdate( driver.Type, driver, ( type, already ) => driver );
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
