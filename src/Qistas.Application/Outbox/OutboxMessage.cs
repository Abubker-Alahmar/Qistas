namespace Qistas.Application.Outbox;

/// <summary>
/// Represents one queued D365 write operation. Written on retry-exhaustion (never
/// dropped), read by the admin review endpoints and the Worker's retry loop
/// (AGENT_INSTRUCTION.md section 5; PLAN.md 1.3/1.6).
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

    public DateTime UpdatedUtc { get; set; }
}

public enum OutboxStatus
{
    Pending,
    Sent,
    Failed,
    Manual,
}
