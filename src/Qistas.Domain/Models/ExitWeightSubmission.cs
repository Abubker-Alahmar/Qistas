namespace Qistas.Domain.Models;

/// <summary>
/// Domain input for the exit-weight (Weight-Out) use case.
/// </summary>
public sealed class ExitWeightSubmission
{
    public required string LoadId { get; init; }
    public required string CompanyId { get; init; }

    /// <summary>Scale operator -- maps to the top-level "Userid" (lower-case) wire field.</summary>
    public required string UserId { get; init; }

    /// <summary>Balance's Transaction GUID -- the idempotency/dedupe key on this call.</summary>
    public required string ScaleSystemReferenceId { get; init; }

    public required decimal EntryWeightKg { get; init; }
    public required decimal ExitWeightKg { get; init; }
    public required decimal TotalNetWeightKg { get; init; }
    public required decimal TotalGrossWeightKg { get; init; }
    public required decimal ToleranceKg { get; init; }
    public required DateTimeOffset ExitDateTimeUtc { get; init; }
}
