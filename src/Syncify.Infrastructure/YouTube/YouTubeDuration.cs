using System.Xml;

namespace Syncify.Infrastructure.YouTube;

internal static class YouTubeDuration
{
    /// <summary>
    /// Reads an ISO 8601 duration such as "PT4M8S". Live streams and premieres report "P0D", which is not a length.
    /// </summary>
    public static TimeSpan? Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var duration = XmlConvert.ToTimeSpan(value);
            return duration > TimeSpan.Zero ? duration : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
