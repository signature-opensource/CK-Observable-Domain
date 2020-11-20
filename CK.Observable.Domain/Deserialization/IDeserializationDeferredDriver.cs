namespace CK.Observable
{
    /// <summary>
    /// Handles deserialization of object in two phases.
    /// </summary>
    public interface IDeserializationDeferredDriver : IDeserializationDriver
    {
        /// <summary>
        /// Allocates an unitialized object.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">The read info.</param>
        /// <returns>The unitialized object.</returns>
        object Allocate( IBinaryDeserializer r, TypeReadInfo readInfo );

        /// <summary>
        /// Deserializes an already allocated object by <see cref="Allocate(IBinaryDeserializer, TypeReadInfo)"/>.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">
        /// The read information of the type as it has been written.
        /// If type based serialization has been used (with versions and ancestors). 
        /// </param>
        /// <param name="o">The object to fill.</param>
        void ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, object o );

    }
}
