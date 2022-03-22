namespace CK.Observable
{
    /// <summary>
    /// Unifies driver information.
    /// </summary>
    public interface IUnifiedTypeDriver
    {
        /// <summary>
        /// Gets the export driver.
        /// Null if no export driver is available.
        /// </summary>
        IObjectExportTypeDriver? ExportDriver { get; }
    }
}
