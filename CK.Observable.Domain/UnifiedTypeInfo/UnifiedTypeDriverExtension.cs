using System;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="IUnifiedTypeDriver"/>.
    /// </summary>
    public static class UnifiedTypeDriverExtension
    {
        /// <summary>
        /// Gets whether this unified driver handles serialization, deserialization and export
        /// and that the <see cref="ITypeSerializationDriver.Type"/> and <see cref="IObjectExportTypeDriver.BaseType"/>
        /// are the same.
        /// </summary>
        /// <param name="this">This driver.</param>
        /// <returns>True if this is a valid and full driver.</returns>
        public static bool IsValidFullDriver( this IUnifiedTypeDriver @this )
        {
            return @this != null
                    && @this.DeserializationDriver != null
                    && @this.ExportDriver != null
                    && @this.SerializationDriver != null
                    && @this.ExportDriver.BaseType.IsAssignableFrom( @this.SerializationDriver.Type );
        }

        /// <summary>
        /// Checks that the unified driver handles serialization, deserialization and export
        /// and that the <see cref="ITypeSerializationDriver.Type"/> and <see cref="IObjectExportTypeDriver.BaseType"/>
        /// are the same: throws an <see cref="ArgumentException"/> with a detailed message if it's not the case.
        /// </summary>
        /// <param name="this">This unified driver.</param>
        /// <returns>The type that is handled.</returns>
        public static Type CheckValidFullDriver( this IUnifiedTypeDriver @this )
        {
            if( @this == null ) throw new ArgumentNullException( "Null unified driver." );
            if( @this.DeserializationDriver == null ) throw new ArgumentException( "Missing DeserializationDriver.", "driver." );
            if( @this.SerializationDriver == null ) throw new ArgumentException( "Missing SerializationDriver.", "driver." );
            if( @this.ExportDriver == null ) throw new ArgumentException( "Missing ExportDriver.", "driver." );
            if( !@this.ExportDriver.BaseType.IsAssignableFrom( @this.SerializationDriver.Type ) ) throw new ArgumentException( $"ExportDriver base type {@this.ExportDriver.BaseType} is not compatible with SerializationDriver.Type {@this.SerializationDriver.Type}.", "driver" );
            return @this.SerializationDriver.Type;
        }
    }
}
