using System.Globalization;
using System.Text.Json;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Xunit;

namespace Qistas.Tests.Serialization;

/// <summary>
/// Scale PCs run Arabic Windows (ar-LY): the default culture there emits Arabic-Indic
/// digits and "٫" as the decimal separator, which would corrupt the D365 payload if any
/// serialization path relied on the current culture instead of InvariantCulture
/// (AGENT_INSTRUCTION.md section 3). These tests run the whole serializer under ar-LY to
/// prove the output is unaffected.
/// </summary>
public class CultureSerializationTests
{
    [Fact]
    public void Serialization_UnderArabicCulture_ProducesInvariantNumbersAndDates()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var arabicCulture = new CultureInfo("ar-LY");
            CultureInfo.CurrentCulture = arabicCulture;
            CultureInfo.CurrentUICulture = arabicCulture;

            var request = new SetExitWeightDetailsRequest
            {
                CompanyId = "Bell",
                Userid = "op",
                LoadId = "L-1",
                ScaleSystemReferenceId = "ref-1",
                EntryWeight = 12345.67m,
                ExitWeight = 23456.78m,
                TotalNetWeight = 11111.11m,
                TotalGrossWeight = 23999.99m,
                Telorence = 5.5m,
            };

            string json = JsonSerializer.Serialize(request, QistasJson.Options);

            // No Arabic-Indic digits (U+0660-U+0669) anywhere in the payload.
            Assert.DoesNotContain(json, c => c is >= '٠' and <= '٩');

            // No Arabic decimal separator (U+066B).
            Assert.DoesNotContain('٫', json);

            // Weights use '.' as the decimal separator with exactly 2 fraction digits.
            Assert.Contains("\"EntryWeight\":12345.67", json);
            Assert.Contains("\"ExitWeight\":23456.78", json);
            Assert.Contains("\"Telorence\":5.50", json);

            // Date formatting (DriverLicenseExpiryDate on the entry contract is a plain ISO
            // string produced by the mapper via InvariantCulture -- verify the underlying
            // date converter used for response round-trips is equally culture-safe).
            var dateOnly = new DateOnly(2029, 3, 26);
            string dateJson = JsonSerializer.Serialize(dateOnly, QistasJson.Options);
            Assert.Equal("\"2029-03-26\"", dateJson);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Deserialization_UnderArabicCulture_ParsesArabicIndicAndCommaWeightsSafely()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var arabicCulture = new CultureInfo("ar-LY");
            CultureInfo.CurrentCulture = arabicCulture;
            CultureInfo.CurrentUICulture = arabicCulture;

            // Simulates a weight value that leaked in with an Arabic decimal separator and
            // Arabic-Indic digits -- the converter must still normalize and parse it.
            string json = "\"١٢٣٤٫٥٦\"";
            decimal value = JsonSerializer.Deserialize<decimal>(json, QistasJson.Options);

            Assert.Equal(1234.56m, value);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
