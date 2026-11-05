using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QuizArena.Statistics.Api.Contracts;

/// <summary>
/// Accepts only RFC 3339 timestamps with an explicit offset ("Z" or "+hh:mm"). Without this, a value such as
/// "2026-11-04T12:00:00" would be read in the server's local time zone, so the stored time would depend on the host.
/// </summary>
public sealed partial class ExplicitOffsetDateTimeConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text is null || !Rfc3339WithOffset().IsMatch(text)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new JsonException("Timestamps must be RFC 3339 with an explicit offset, e.g. 2026-11-04T12:00:00Z.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex Rfc3339WithOffset();
}
