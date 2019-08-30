namespace CK.Observable
{
    /// <summary>
    /// Handles deserialization from a type's assembly qualified name.
    /// </summary>
    public interface IDeserializationDriver
    {
        /// <summary>
        /// Gets the type's assembly qualified name that this driver handles.
        /// </summary>
        string AssemblyQualifiedName { get; }

        /// <summary>
        /// Reads the data and instanciates a new object.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">
        /// The read information of the type as it has been written.
        /// If type based serialization has been used (with versions and ancestors). 
        /// </param>
        /// <returns>Must return the new instance.</returns>
        object ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  );

    }
}
