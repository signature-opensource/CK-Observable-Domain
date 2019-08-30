namespace CK.Observable
{
    public interface ICtorBinaryDeserializer : IBinaryDeserializer
    {
        /// <summary>
        /// Get the type based information as it has been written.
        /// If the object has been written by an external driver, this is null.
        /// </summary>
        TypeReadInfo CurrentReadInfo { get; }

    }
}
