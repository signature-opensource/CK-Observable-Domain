using CK.Core;
using System;

namespace CK.Observable;

/// <summary>
/// Export drivers for a type.
/// <para>
/// This DOESN'T handle binary serialization. The name has been kept for backward compatibility.
/// </para>
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IUnifiedTypeDriver<T> : IUnifiedTypeDriver
{
    /// <summary>
    /// Gets the type handled.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the export driver.
    /// Null if no export driver is available.
    /// </summary>
    new IObjectExportTypeDriver<T>? ExportDriver { get; }
}
