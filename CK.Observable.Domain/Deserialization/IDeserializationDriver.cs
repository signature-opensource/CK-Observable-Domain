namespace CK.Observable
{
    /// <summary>
    /// Handles deserialization from a type's assembly qualified name.
    /// Just like <see cref="ITypeSerializationDriver"/> is not bound to a Type, a deserializer
    /// is not bound to a <see cref="System.Type.AssemblyQualifiedName"/>: it is the <see cref="DeserializerRegistry"/>
    /// that is in charge of the mapping from the "name" to the deserializer to use.
    /// </summary>
    public interface IDeserializationDriver
    {
        /// <summary>
        /// Reads the data and instantiates a new object.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">
        /// The read information of the type as it has been written.
        /// If type based serialization has been used (with versions and ancestors). 
        /// </param>
        /// <returns>Must return the new instance.</returns>
        object? ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead );

    }
}
