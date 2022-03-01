namespace CK.Observable
{
    /// <summary>
    /// Unifies driver information.
    /// </summary>
    public interface IUnifiedTypeDriver
    {
        /// <summary>
        /// Gets the deserialization driver.
        /// Null if no deserialization driver is available.
        /// </summary>
        IDeserializationDriver? DeserializationDriver { get; }

        /// <summary>
        /// Gets the export driver.
        /// Null if no export driver is available.
        /// </summary>
        IObjectExportTypeDriver? ExportDriver { get; }
    }
}
