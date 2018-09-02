using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Associates a version to a <see cref="TypeSerializationKind.TypeBased"/> class or struct.
    /// This attribute is required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class SerializationVersionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new version attribute.
        /// </summary>
        /// <param name="version">The version. Must be positive or zero.</param>
        public SerializationVersionAttribute( int version )
        {
            if( version < 0 ) throw new ArgumentException( "Must be 0 or positive.", nameof(version) );
            Version = version;
        }

        /// <summary>
        /// Gets the version.
        /// </summary>
        public int Version { get; }
    }
}
