namespace CK.Observable
{
    /// <summary>
    /// Defines the basic context provided to deserialization constructors.
    /// </summary>
    public interface IBinaryDeserializerContext
    {
        /// <summary>
        /// Only exposed method that must be called at the very beginning of the
        /// deserialization constructor in order to obtain the deserializer to use.
        /// </summary>
        /// <returns>The deserializer and <see cref="TypeReadInfo"/> to use.</returns>
         CtorReader StartReading();
    }
}
