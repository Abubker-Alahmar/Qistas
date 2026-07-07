using Qistas.Application.Abstractions;
using Qistas.Application.Mapping;
using Qistas.Domain.Contracts;
using Qistas.Domain.Models;

namespace Qistas.Application.UseCases;

/// <summary>
/// Use case for the Weight-Out screen open: ALWAYS fetches a fresh getLoadDetails
/// immediately before validation -- items/qty may have changed in D365 during loading,
/// so the entry-time snapshot must never be reused (Balance/CLAUDE.md #7 / #16.7).
/// </summary>
public sealed class GetLoadForValidationHandler
{
    private readonly ID365Client _client;
    private readonly IActiveEnvironmentProvider _environmentProvider;

    public GetLoadForValidationHandler(ID365Client client, IActiveEnvironmentProvider environmentProvider)
    {
        _client = client;
        _environmentProvider = environmentProvider;
    }

    public async Task<LoadValidationResult> HandleAsync(string loadId, string userId, CancellationToken cancellationToken)
    {
        var environment = _environmentProvider.GetActiveEnvironment();
        var settings = _environmentProvider.GetSettings(environment);

        var request = new GetLoadDetailsRequest
        {
            CompanyId = settings.CompanyId,
            Userid = userId,
            LoadId = loadId,
        };
        var callResult = await _client.GetLoadDetailsAsync(request, environment, cancellationToken);

        if (!callResult.TransportSucceeded)
        {
            return new LoadValidationResult
            {
                Success = false,
                Message = $"Could not reach D365 ({callResult.TransportError}).",
            };
        }

        var result = LoadDetailsMapper.ToDomainResult(callResult.Response, callResult.TransportError);

        if (result.Success && !string.IsNullOrWhiteSpace(callResult.Response?.CompanyId))
        {
            // CompanyId may be echoed back in a different case ("BELL" vs "Bell") -- compare
            // case-insensitively rather than treating a casing difference as a mismatch.
            bool companyMatches = D365ResponseSemantics.CompanyIdMatches(settings.CompanyId, callResult.Response.CompanyId);
            if (!companyMatches)
            {
                return new LoadValidationResult
                {
                    Success = false,
                    Message = $"Load belongs to a different company ({callResult.Response.CompanyId}).