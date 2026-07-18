using System.Text.Encodings.Web;
using System.Text.Json;

namespace Qistas.Domain.Json;

// Single shared JsonSerializerOptions instance used everywhere Qistas serializes or
// deserializes D365 contract payloads. Culture-safe (invariant), tolerant of unknown
// "$id" DataContract metadata members, and wires up the decimal/date converters required
// to keep the contract typos and quirks intact (see AGENT_INSTRUCTION.md, section 2/3).
public static class QistasJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            // Unknown members ($id, $values, etc. from the DataContract serializer) are
            // ignored by default in System.Text.Json -- kept as the explicit default.
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        };

        options.Converters.Add(new TwoDecimalConverter());
        options.Converters.Add(new NullableTwoDecimalConverter());
        options.Converters.Add(new LenientNullableDateTimeConverter());
        options.Converters.Add(new LenientDateOnlyStringConverter());

        return options;
    }
}
