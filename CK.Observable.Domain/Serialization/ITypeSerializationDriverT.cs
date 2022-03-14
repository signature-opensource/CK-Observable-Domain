namespace CK.Observable
{
    /// <summary>
    /// Strongly typed specialization of <see cref="ITypeSerializationDriver"/>.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    public interface ITypeSerializationDriver<T> : ITypeSerializationDriver
    {
        /// <summary>
        /// Writes the object's data.
        /// </summary>
        /// <param name="w">The serializer.</param>
        /// <param name="o">The object instance.</param>
        void WriteData( BinarySerializer w, T o );

    }
}
