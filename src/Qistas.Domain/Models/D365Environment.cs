namespace Qistas.Domain.Models;

/// <summary>
/// The three D365FO environments Qistas can target. A token cached for one environment
/// must never be sent to another -- the cache key is always this enum
/// (AGENT_INSTRUCTION.md section 4; Balance/CLAUDE.md #16.20).
/// </summary>
public enum D365Environment
{
    Dev,
    Test,
    Prod,
}
