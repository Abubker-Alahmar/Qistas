using Qistas.Api.Contracts;
using Qistas.Application.Abstractions;
using Qistas.Application.UseCases;
using Qistas.Domain.Models;

namespace Qistas.Api.Endpoints;

/// <summary>
/// Balance-facing endpoints -- what the WinForms app calls over localhost HTTP at the
/// three integration points (Balance/CLAUDE.md section 14, call points 1-3). All tagged
/// "balance" so they appear only in the "Balance API" Swagger document (PLAN.md 1.5).
/// </summary>
public static class BalanceEndpoints
{
    public static IEndpointRouteBuilder MapBalanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scale")
            .WithGroupName("balance")
            .WithTags("Scale");

        group.MapPost("/entry-weight", PostEntryWeight)
            .WithName("PostEntryWeight")
            .WithSummary("Submit Weight-In (setEntryWeightDetails) for a Sales Order load.");

        group.MapGet("/loads/{loadId}", GetLoadDetails)
            .WithName("GetLoadDetails")
            .WithSummary("Fetch fresh load lines for Weight-Out validation (never use the entry-time snapshot).");

        group.MapPost("/exit-weight", PostExitWeight)
            .WithName("PostExitWeight")
            .WithSummary("Submit Weight-Out (setExitWeightDetails), idempotent on ScaleSystemReferenceId.");

        app.MapGet("/api/health", GetHealth)
            .WithGroupName("balance")
            .WithTags("Health")
            .WithName("GetHealth")
            .WithSummary("Token status + active environment; add ?probe=true for live Azure AD and D365 reachability (separate failure states).");

        return app;
    }

    private static async Task<IResult> PostEntryWeight(
        EntryWeightRequestDto dto,
        SubmitEntryWeightHandler handler,
        CancellationToken cancellationToken)
    {
        var submission = EntryWeightRequestDto.ToDomain(dto);
        var result = await handler.HandleAsync(submission, cancellationToken);
        return result.Success ? Results.Ok(ApiResultDto.From(result)) : Results.UnprocessableEntity(ApiResultDto.From(result));
    }

    private static async Task<IResult> GetLoadDetails(
        string loadId,
        string? operatorUserId,
        GetLoadForValidationHandler handler,
        CancellationToken cancellationToken)
    {
        // operatorUserId comes as a query parameter (?operatorUserId=...); it maps to the
        // top-level "Userid" wire field. Defaults to "Balance" when not supplied.
        var result = await handler.HandleAsync(loadId, operatorUserId ?? "Balance", cancellationToken);
        return result.Success ? Results.Ok(LoadValidationResultDto.From(result)) : Results.NotFound(LoadValidationResultDto.From(result));
    }

    private static async Task<IResult> PostExitWeight(
        ExitWeightRequestDto dto,
        SubmitExitWeightHandler handler,
        GetLoadForValidationHandler loadHandler,
        CancellationToken cancellationToken)
    {
        // Always re-fetch getLoadDetails immediately before validating the exit call --
        // never trust a client-supplied/cached snapshot (Balance/CLAUDE.md #16.7).
        var freshLoad = await loadHandler.HandleAsync(dto.LoadId, dto.OperatorUserId, cancellationToken);
        var submission = ExitWeightRequestDto.ToDomain(dto);
        var result = await handler.HandleAsync(submission, freshLoad, cancellationToken);
        return result.Success ? Results.Ok(ApiResultDto.From(result)) : Results.UnprocessableEntity(ApiResultDto.From(result));
    }

    private static async Task<IResult> GetHealth(
        ITokenService tokenService,
        IActiveEnvironmentProvider environmentProvider,
        IHttpClientFactory httpClientFactory,
        bool probe,
        CancellationToken cancellationToken)
    {
        var environment = environmentProvider.GetActiveEnvironment();
        var settings = environmentProvider.GetSettings(environment);
        var tokenStatus =