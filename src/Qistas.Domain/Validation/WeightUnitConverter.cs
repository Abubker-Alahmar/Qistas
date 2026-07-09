using System.Globalization;

namespace Qistas.Domain.Validation;

/// <summary>
/// Normalizes weights to KG. Balance has a KG_TONNE setting and D365 load lines carry a
/// UnitId; Qistas always converts to KG with 2 decimals before comparing or sending
/// (Balance/CLAUDE.md #16.11).
/// </summary>
public static class WeightUnitConverter
{
    private const decimal KgPerTonne = 1000m;

    public static decimal ToKg(decimal value, string? unitId)
    {
        string unit = (unitId ?? "KG").Trim().ToUpper(CultureInfo.InvariantCulture);

        decimal kg = unit switch
        {
            "KG" => value,
            "TONNE" or "TON" or "T" or "MT" => value * KgPerTonne,
            "G" or "GRAM" or "GRAMS" => value / 1000m,
            _ => value, // Unknown unit: assume already KG rather than silently corrupting data.
        };

        return Math.Round(kg, 2, MidpointRounding.AwayFromZero);
    }
}
