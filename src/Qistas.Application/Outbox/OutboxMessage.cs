namespace Qistas.Application.Outbox;

/// <summary>
/// One ARCHIVED D365 write operation. Written when inline Polly retries are exhausted
/// (never dropped) or as a Sent audit row. Read by the admin review endpoints only --
/// an employee re-sends ("Retry now") or resolves ("Mark manual"); there is no automatic
/// background retry (AGENT_INSTRUCTION.md section 5; PLAN.md 1.6).
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; set; }

    /// <summary>Balance Transaction GUID -- dedupe key for exit-weight idempotency.</summary>
    public required string ScaleSystemReferenceId { get; set; }

    /// <summary>One of: setEntryWeightDetails, getLoadDetails, setExitWeightDetails.</summary>
    public required string Operation { get; set; }

    public required string Environment { get; set; }

    public required string PayloadJson { get; set; }

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public string? LastResponseJson { get; set; }

    public DateTime CreatedUtc { get; set; }

    pub