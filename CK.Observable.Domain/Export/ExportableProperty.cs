using System;

namespace CK.Observable;

/// <summary>
/// Small type definition of an object's property used by <see cref="ObjectExporter"/>.
/// </summary>
public class ExportableProperty
{
    /// <summary>
    /// Gets the type that declares this property.
    /// </summary>
    public Type DeclaringType { get; }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the property value.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new <see cref="ExportableProperty"/>.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <param name="n">The proprty name.</param>
    /// <param name="v">The property value.</param>
    public ExportableProperty( Type t, string n, object v )
    {
        DeclaringType = t;
        Name = n;
        Value = v;
    }
}
