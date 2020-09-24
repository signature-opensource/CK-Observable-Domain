using System;

namespace CK.Observable
{
    /// <summary>
    /// Handles serialization of instances of a given type.
    /// Note that this abstraction doesn't require to be bound to a Type: the <see cref="SerializerRegistry"/>
    /// is in charge of the association from a Type to a driver.
    /// </summary>
    public interface ITypeSerializationDriver
    {
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
