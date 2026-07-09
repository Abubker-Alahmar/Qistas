using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qistas.Domain.Json;

/// <summary>
/// Parses D365 date/time responses leniently:
///   - Accepts non-padded formats such as "2029-3-26" and full ISO with time.
///   - Treats the "1900-01-01T12:00:00" sentinel (and any 1900-01-01 date, any time)
///     as "no value" -&gt; null (AGENT_INSTRUCTION.md, section 2 / Balance/CLAUDE.md #15).
///   - Writes ISO "yyyy-MM-dd" (date only, as required by the outbound contract) using
///     InvariantCulture; writes null as JSON null.
/// </summary>
public sealed class LenientNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly string[] AcceptedFormats =
    {
        "yyyy-M-d'T'H:m:s",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-M-d",
        "yyyy-MM-dd",
        "yyyy-M-d'T'H:m:s.fff",
        "yyyy-MM-dd'T'HH:mm:ss.fff",
    };

    public static readonly DateTime SentinelDate = new(1900, 1, 1);

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        string? raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        DateTime parsed;
        if (!DateTime.TryParseExact(raw, AcceptedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed))
        {
            // Last-resort lenient parse (still culture-invariant) for any other shape D365 sends.
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return null;
            }
        }

        return IsSentinel(parsed) ? null : parsed;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public static bool IsSentinel(DateTime value) => value.Date == SentinelDate;
}

/// <summary>
/// Non-nullable variant for fields that are always ISO "yyyy-MM-dd" on the way out
/// (e.g. license expiry dates in the entry-weight request) but may still need lenient
/// parsing on the way in from a round-trip/echo response.
/// </summary>
public sealed class LenientDateOnlyStringConverter : JsonConverter<DateOnly>
{
    private static readonly string[] AcceptedFormats =
    {
        "yyyy-M-d",
        "yyyy-MM-dd",
    };

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string raw = reader.GetString() ?? throw new JsonException("Expected a date string.");

        if (DateOnly.TryParseExact(raw, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        throw new JsonException($"Unable to parse date '{raw}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
