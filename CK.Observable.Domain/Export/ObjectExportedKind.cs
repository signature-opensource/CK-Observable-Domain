using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Defines the 4 fundamental kind of objects that are exported.
    /// </summary>
    public enum ObjectExportedKind
    {
        /// <summary>
        /// Non applicable.
        /// </summary>
        None = 0,

        /// <summary>
        /// Basic objects.
        /// </summary>
        Object = 1,

        /// <summary>
        /// Indexed set of objects like list or objects.
        /// </summary>
        List = 2,

        /// <summary>
        /// Associative map between keys and values like <see cref="Dictionary{TKey,TValue}"/>.
        /// </summary>
        Map = 3,

        /// <summary>
        /// Set of objects like bags or unordered sets.
        /// </summary>
        Set = 4
    }
}
