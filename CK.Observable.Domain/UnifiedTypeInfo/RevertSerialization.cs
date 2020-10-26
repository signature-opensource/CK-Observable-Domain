namespace CK.Observable
{
    /// <summary>
    /// This type is a marker used by the "empty reversed deserializer constructor".
    /// </summary>
    public class RevertSerialization
    {
        /// <summary>
        /// The only instance.
        /// </summary>
        public static readonly RevertSerialization Default = new RevertSerialization();

        RevertSerialization() {}
    }
}
