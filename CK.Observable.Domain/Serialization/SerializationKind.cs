using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Describes the kind of serialization a Type supports. 
    /// </summary>
    [Flags]
    public enum TypeSerializationKind
    {
        /// <summary>
        /// The type is not serializable.
        /// </summary>
        None,

        /// <summary>
        /// The type is serializable, either natively (<see cref="TypeBased"/>) or
        /// via an external driver.
        /// </summary>
        Serializable = 1,

        /// <summary>
        /// The type (and its base types) supports serialization directly.
        /// </summary>
        TypeBased = Serializable | 2,

        /// <summary>
        /// The type support serialization thanks to an external driver.
        /// </summary>
        External = Serializable | 4
    }
}
