namespace Qistas.Domain.Models;

/// <summary>
/// Domain input for the entry-weight (Weight-In) use case. Only Sales Order loads
/// (BuySell = "مبيعات") with a LoadId should ever reach this (AGENT_INSTRUCTION.md
/// section "Scope"; Balance/CLAUDE.md #6 in Part 2 section 14).
/// </summary>
public sealed class EntryWeightSubmission
{
    public required string LoadId { get; init; }
    public required string CompanyId { get; init; }

    /// <summary>Scale operator -- maps to the top-level "Userid" (lower-case) wire field.</summary>
    public required string UserId { get; init; }

    /// <summary>Balance's Transaction GUID. Not sent on the entry call (exit only), but
    /// kept for outbox correlation.</summary>
    public required string ScaleSystemReferenceId { get; init; }

    public required decimal EntryWeightKg { get; init; }
    public required DateTimeOffset EntryDateTimeUtc { get; init; }

    public required DriverInfo Driver { get; init; }
    public required VehicleInfo Vehicle { get; init; }
}
