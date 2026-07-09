using Qistas.Domain.Contracts;
using Qistas.Domain.Models;

namespace Qistas.Application.Abstractions;

/// <summary>
/// Wire-level client for the three D365FO BTOLoadIntService operations. Deals purely in
/// contract DTOs (Qistas.Domain.Contracts) -- domain &lt;-&gt; contract mapping happens in
/// the Application use cases (AGENT_INSTRUCTION.md section 8). Implemented in
/// Qistas.Infrastructure with Polly resilience + 401 refresh-and-retry-once.
/// </summary>
public interface ID365Client
{
    // All three operations share the common flat response shape (D365Response):
    // "$id" / "Context" / "CompanyId" / "Status" / "Message" -- APIs V2.0 Common Response Scheme.
    Task<D365CallResult<D365Response>> SetEntryWeightDetailsAsync(
        SetEntryWeightDetailsRequest request, D365Environment environment, CancellationToken cancellationToken);

    Task<D365CallResult<D365Response>> GetLoadDetailsAsync(
        GetLoadDetailsRequest request, D365Environment environment, CancellationToken cancellationToken);

    Task<D365CallResult<D365Response>> SetExitWeightDetailsAsync(
        SetExitWeightDetailsRequest request, D365Environment environment, CancellationToken cancellationToken);
}

/// <summary>
/// Transport-level outcome: distinguishes "we got a response from D365" (which may itself
/// carry Status=false) from "the call itself failed" (network/timeout/exhausted retries).
/// </summary>
public sealed class D365CallResult<TResponse>
{
    public required bool TransportSucceeded { get; init; }
    public TResponse? Response { get; init; }
    public string? RawResponseJson { get; init; }
    public string? TransportError { get; init; }

    public static D365CallResult<TResponse> Ok(TResponse response, string rawJson) => new()
    {
        TransportSucceeded = true,
        Response = response,
        RawResponseJson = rawJson,
    };

    public static D365CallResult<TResponse> TransportFailure(string error) => new()
    {
        TransportSucceeded = false,
        TransportError = error,
    };
}
