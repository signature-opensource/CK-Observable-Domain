namespace CK.Observable
{
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
