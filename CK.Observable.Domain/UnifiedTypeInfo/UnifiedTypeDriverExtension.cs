using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public static class UnifiedTypeDriverExtension
    {
        public static bool IsValidFullDriver( this IUnifiedTypeDriver @this )
        {
            return @this != null
                    && @this.DeserializationDriver != null
                    && @this.ExportDriver != null
                    && @this.SerializationDriver != null
                    && @this.SerializationDriver.Type == @this.ExportDriver.Type;
        }

        /// <summary>
        /// Checks that the unified driver handles serialization, deserialization and export
        /// and that the <see cref="ITypeSerializationDriver.Type"/> and <see cref="IObjectExportTypeDriver.Type"/>
        /// are the same.
        /// </summary>
        /// <param name="this">This unified driver.</param>
        /// <returns>The type that is handled.</returns>
        public static Type CheckValidFullDriver( this IUnifiedTypeDriver @this )
        {
            if( @this == null ) throw new ArgumentNullException( "Null unified driver." );
            if( @this.DeserializationDriver == null ) throw new ArgumentException( "Missing DeserializationDriver.", "driver." );
            if( @this.SerializationDriver == null ) throw new ArgumentException( "Missing SerializationDriver.", "driver." );
            if( @this.ExportDriver == null ) throw new ArgumentException( "Missing ExportDriver.", "driver." );
            if( @this.SerializationDriver.Type != @this.ExportDriver.Type ) throw new ArgumentException( "SerializationDriver and ExportDriver don't handle the same Type.", "driver." );
            return @this.SerializationDriver.Type;
        }
    }
}
