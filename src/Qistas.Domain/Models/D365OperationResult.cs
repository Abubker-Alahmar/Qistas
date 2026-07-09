namespace Qistas.Domain.Models;

/// <summary>
/// Normalized outcome of any D365 write operation (entry/exit weight). Wraps the raw
/// Status/Message plus the ghost-success/duplicate classification needed for idempotent
/// retries (AGENT_INSTRUCTION.md section 6).
/// </summary>
public sealed class D365OperationResult
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public bool WasAlreadyProcessed { get; init; }
    public string? RawResponseJson { get; init; }

    public static D365OperationResult Ok(string? message, string? rawJson, bool alreadyProcessed = false) =>
        new() { Success = true, Message = message, RawResponseJson = rawJson, WasAlreadyProcessed = alreadyProcessed };

    public static D365OperationResult Fail(string? message, string? rawJson) =>
        new() { Success = false, Message = message, RawResponseJson = rawJson };
}
