namespace Qistas.Domain.Models;

/// <summary>
/// Domain-side view of a getLoadDetails response, used by Balance to display lines and
/// perform the local tolerance validation before Weight-Out save (Balance/CLAUDE.md
/// section 14, call point 2). Always fetched fresh immediately before validation -- never
/// cached across the loading window (Balance/CLAUDE.md #16.7).
/// </summary>
public sealed class LoadValidationResult
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? LoadId { get; init; }
    public string? CompanyId { get; init; }
    public string? SalesId { get; init; }
    public string? CustAccount { get; init; }
    public string? CustName { get; init; }
    public decimal? HeaderNetWeightKg { get; init; }
    public decimal? HeaderGrossWeightKg { get; init; }
    public IReadOnlyList<LoadLineInfo> Lines { get; init; } = Array.Empty<LoadLineInfo>();

    public decimal TotalLineNetWeightKg => Lines.Sum(l => l.NetWeightKg);
    public decimal TotalLineGrossWeightKg => Lines.Sum(l => l.GrossWeightKg);
}

public sealed class LoadLineInfo
{
    public required string ItemId { get; init; }
    public string? ItemName { get; init; }
    public string? ItemDescription { get; init; }
    public string? BatchNumber { get; init; }
    public decimal QuantityKg { get; init; }
    public decimal NetWeightKg { get; init; }
    public decimal GrossWeightKg { get; init; }
}
