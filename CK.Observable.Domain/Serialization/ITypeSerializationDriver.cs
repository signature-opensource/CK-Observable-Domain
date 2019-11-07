using System;

namespace CK.Observable
{
    /// <summary>
    /// Handles serialization for instances of a given type.
    /// </summary>
    public interface ITypeSerializationDriver
    {
        /// <summary>
        /// Gets the type that this driver handles.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets whether this type cannot have any subordinate types.
        /// </summary>
        bool IsFinalType { get; }

        /// <summary>
        /// Writes the type descriptor in the serializer.
        /// </summary>
        /// <param name="s">The serializer.</param>
        void WriteTypeInformation( BinarySerializer s ); 

        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        void WriteData( BinarySerializer w, object o );

    }
}
