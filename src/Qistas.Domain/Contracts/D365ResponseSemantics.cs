namespace Qistas.Domain.Contracts;

/// <summary>
/// Shared helpers for interpreting D365 response semantics that are easy to get wrong:
/// CompanyId case-insensitivity, and "already processed" duplicate detection for the
/// idempotent exit-weight call (AGENT_INSTRUCTION.md section 6; Balance/CLAUDE.md #16.3).
/// </summary>
public static class D365ResponseSemantics
{
    /// <summary>
    /// D365 may echo CompanyId back in a different case ("Bell" sent, "BELL" returned).
    /// Always compare case-insensitively (ordinal, culture independent).
    /// </summary>
    public static bool CompanyIdMatches(string? sent, string? received) =>
        string.Equals(sent?.Trim(), received?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Best-effort detection of "this load/reference was already processed" style
    /// messages so a retry after a ghost-success timeout can be treated as success
    /// rather than a hard failure. D365 does not define a single canonical message, so
    /// this checks for common phrasing; the outbox still records the raw message for
    /// human review if the check doesn't match.
    /// </summary>
    public static bool IndicatesAlreadyProcessed(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = message.ToLowerInvariant();
        return normalized.Contains("already processed")
            || normalized.Contains("already exists")
            || normalized.Contains("duplicate")
            || normalized.Contains("already submitted")
            || normalized.Contains("already sent");
    }
}
