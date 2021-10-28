namespace CK.Observable
{
    /// <summary>
    /// Handles deserialization from a type's assembly qualified name.
    /// </summary>
    public interface IDeserializationDriver<T> : IDeserializationDriver
    {
        /// <summary>
        /// Reads the data and instantiates a new object.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">
        /// The read information (with versions and ancestors if type based serialization has been used) of the
        /// type as it has been written. Null when type is known by design.
        /// </param>
        /// <returns>The new instance.</returns>
        new T ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead );

    }
}
