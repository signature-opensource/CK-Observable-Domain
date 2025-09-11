using System;
using System.Globalization;

namespace CK.Observable;

/// <summary>
/// Basic JSON export strategy.
/// </summary>
public class JsonSerializerOptions
{
    /// <summary>
    /// Default that should always be used.
    /// </summary>
    internal static readonly JsonSerializerOptions Default = new JsonSerializerOptions();

    /// <summary>
    /// Gets or sets the culture. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public CultureInfo CultureInfo { get; set; }

    /// <summary>
    /// Converter for <see cref="DateTime"/>. Default uses the "o" format.
    /// </summary>
    public Func<DateTime, string> DateTimeConverter { get; set; }

    /// <summary>
    /// Converter for <see cref="DateTimeOffset"/>. Default uses the "o" format.
    /// </summary>
    public Func<DateTimeOffset, string> DateTimeOffsetConverter { get; set; }

    /// <summary>
    /// Converter for <see cref="TimeSpan"/>. Default uses the simple <see cref="TimeSpan.ToString()"/>.
    /// </summary>
    public Func<TimeSpan, string> TimeSpanConverter { get; set; }

    /// <summary>
    /// Initializes a new default <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public JsonSerializerOptions()
    {
        CultureInfo = CultureInfo.InvariantCulture;
        DateTimeConverter = o => o.ToString( "o", CultureInfo );
        DateTimeOffsetConverter = o => o.ToString( "o", CultureInfo );
        TimeSpanConverter = o => o.ToString();
    }

}
