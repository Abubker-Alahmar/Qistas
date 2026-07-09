using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qistas.Domain.Json;

/// <summary>
/// Writes decimal values (weights) as JSON numbers with exactly two fraction digits,
/// using InvariantCulture. D365 contract requires numeric weights, never strings
/// (AGENT_INSTRUCTION.md, section 2). Reads tolerate numbers or numeric strings, and
/// tolerate Arabic-Indic digits / comma decimal separators that may leak in from an
/// Arabic-locale caller, by normalizing before parsing.
/// </summary>
public sealed class TwoDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        string raw = reader.GetString() ?? "0";
        return DecimalCultureHelper.ParseInvariant(raw);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(DecimalCultureHelper.ToFixedTwoDecimal(value));
    }
}

/// <summary>
/// Nullable counterpart of <see cref="TwoDecimalConverter"/>.
/// </summary>
public sealed class NullableTwoDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        string? raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DecimalCultureHelper.ParseInvariant(raw);
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(DecimalCultureHelper.ToFixedTwoDecimal(value.Value));
    }
}

/// <summary>
/// Shared normalization for numeric strings that may have been produced under an
/// Arabic-Windows culture (Arabic-Indic digits 0x0660-0x0669, Arabic decimal separator
/// U+066B). Always resolves to InvariantCulture semantics before parsing.
/// </summary>
internal static class DecimalCultureHelper
{
    public static decimal ParseInvariant(string raw)
    {
        string normalized = NormalizeDigits(raw).Replace('٫', '.').Replace(',', '.').Trim();
        return decimal.Parse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Rounds to 2 decimal places and forces the decimal's internal scale to exactly 2
    /// (by round-tripping through an invariant "F2" string) so that
    /// <see cref="Utf8JsonWriter.WriteNumberValue(decimal)"/> emits trailing zeros
    /// (e.g. "1500.00" rather than "1500").
    /// </summary>
    public static decimal ToFixedTwoDecimal(decimal value)
    {
        decimal rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return decimal.Parse(rounded.ToString("F2", CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static string NormalizeDigits(string input)
    {
        Span<char> buffer = input.Length <= 256 ? stackalloc char[input.Length] : new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            buffer[i] = c is >= '٠' and <= '٩'
                ? (char)('0' + (c - '٠'))
                : c;
        }

        return new string(buffer);
    }
}
