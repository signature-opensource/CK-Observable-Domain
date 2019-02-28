using System;
using System.Globalization;

namespace CK.Observable
{
    public class JsonSerializerOptions
    {
        internal static readonly JsonSerializerOptions Default = new JsonSerializerOptions { CultureInfo = CultureInfo.InvariantCulture };

        public CultureInfo CultureInfo { get; set; }

        public Func<DateTime, string> DateTimeConverter { get; set; }

        public Func<DateTimeOffset, string> DateTimeOffsetConverter { get; set; }

        public Func<TimeSpan, string> TimeSpanConverter { get; set; }

        public JsonSerializerOptions()
        {
            DateTimeConverter = o => o.ToString( CultureInfo );
            DateTimeOffsetConverter = o => o.ToString( CultureInfo );
            TimeSpanConverter = o => o.ToString();
        }

    }
}
