using Qistas.Application.Abstractions;
using Qistas.Domain.Contracts;
using Qistas.Domain.Models;

namespace Qistas.Tests.Fakes;

/// <summary>
/// Hand-rolled fake for <see cref="ID365Client"/>. Each operation's result is configured
/// independently and call counts are tracked so use-case tests can assert D365 was (or was
/// not) actually called.
/// </summary>
public sealed class FakeD365Client : ID365Client
{
    public int EntryCallCount { get; private set; }
    public int GetLoadCallCount { get; private set; }
    public int ExitCallCount { get; private set; }

    public Func<SetEntryWeightDetailsRequest, D365CallResult<D365Response>>? EntryResultFactory { get; set; }
    public Func<GetLoadDetailsRequest, D365CallResult<D365Response>>? GetLoadResultFactory { get; set; }
    public Func<SetExitWeightDetailsRequest, D365CallResult<D365Response>>? ExitResultFactory { get; set; }

    public SetEntryWeightDetailsRequest? LastEntryRequest { get; private set; }
    public GetLoadDetailsRequest? LastGetLoadRequest { get; private set; }
    public SetExitWeightDetailsRequest? LastExitRequest { get; private set; }

    public Task<D365CallResult<D365Response>> SetEntryWeightDetailsAsync(
        SetEntryWeightDetailsRequest request, D365Environment environment, CancellationToken cancellationToken)
    {
        EntryCallCount++;
        LastEntryRequest = request;
        var result = EntryResultFactory?.Invoke(request)
            ?? D365CallResult<D365Response>.TransportFailure("No fake result configured.");
        return Task.FromResult(result);
    }

    public Task<D365CallResult<D365Response>> GetLoadDetailsAsync(
        GetLoadDetailsRequest request, D365Environment environment, CancellationToken cancellationToken)
    {
        GetLoadCallCount++;
        LastGetLoadRequest = request;
        var result = GetLoadResultFactory?.Invoke(request)
            ?? D365CallResult<D365Response>.TransportFailure("No fake result configured.");
        return Task.FromResult(result);
    }

    public Task<D365CallResult<D365Response>> SetExitWeightDetailsAsync(
        SetExitWeightDetailsRequest request, D365Environment environment, CancellationToken cancellationToken)
    {
        ExitCallCount++;
        LastExitRequest = request;
        var result = ExitResultFactory?.Invoke(request)
            ?? D365CallResult<D365Response>.TransportFailure("No fake result configured.");
        return Task.FromResult(result);
    }
}
