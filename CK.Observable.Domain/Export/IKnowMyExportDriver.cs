namespace CK.Observable;

/// <summary>
/// Optimization helper that enables exportable objects to cache
/// their associated <see cref="ExportDriver"/>.
/// </summary>
public interface IKnowMyExportDriver
{
    /// <summary>
    /// Gets the export driver to use.
    /// </summary>
    IObjectExportTypeDriver? ExportDriver { get; }
}
