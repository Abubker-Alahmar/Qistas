using Qistas.Application.Abstractions;
using Qistas.Application.Outbox;
using Qistas.Application.UseCases;
using Qistas.Domain.Models;
using Qistas.Infrastructure.Options;

namespace Qistas.Api.Endpoints;

/// <summary>
/// Developer/admin endpoints: outbox review + retry/manual actions, non-secret config
/// snapshot, token status. Tagged "admin" so they appear only in the "Qistas Developer
/// API" Swagger document (PLAN.md 1.5). This is also the backing API for Balance's
/// planned admin settings screen and failed-message review screen
/// (Balance/CLAUDE.md section 14, items 3-4).
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithGroupName("admin")
            .WithTags("Admin");

        group.MapGet("/outbox", GetOutbox)
            .WithName("GetOutbox")
            .WithSummary("List outbox messages, optionally filtered by status.");

        group.MapPost("/outbox/{id:long}/retry", RetryOutboxMessage)
            .WithName("RetryOutboxMessage")
            .WithSummary("Manually retry a single outbox message now.");

        group.MapPost("/outbox/{id:long}/manual", MarkOutboxManual)
            .WithName("MarkOutboxManual")
            .WithSummary("Mark an outbox message as resolved manually (excludes it from further automated retries).");

        group.MapGet("/config", GetConfig)
            .WithName("GetConfig")
            .WithSummary("Non-secret configuration snapshot for every environment.");

        group.MapPut("/config", PutConfig)
            .WithName("PutConfig")
            .WithSummary("Switch the active D365 environment (Dev/Test/Prod) at runtime. Never accepts secrets.");

        group.MapGet("/token-status", GetTokenStatus)
            .WithName("GetTokenStatus")
            .WithSummary("Cached-token status for every environment.");

        return app;
    }

    private static async Task<IResult> GetOutbox(
        string? status,
        IOutboxRepository outboxRepository,
        CancellationToken cancellationToken)
    {
        OutboxStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OutboxStatus>(status, ignoreCase: true, out var parsed))
            {
                return Results.BadRequest(new { error = $"Unknown status '{status}'." });
            }

            filter = parsed;
        }

        var messages = await outboxRepository.GetAllAsync(filter, take: 200, cancellationToken);
        return Results.Ok(messages);
    }

    private static async Task<IResult> RetryOutboxMessage(
        long id,
        RetryOutboxMessageHandler handler,
        Microsoft.Extensions.Options.IOptionsMonitor<QistasOptions> options,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, options.CurrentValue.Retry.MaxAttempts, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }

    private static async Task<IResult> MarkOutboxManual(
        long id,
        MarkOutboxManualHandler handler,
        CancellationToken cancellationToken)
    {
        var found = await handler.HandleAsync(id, cancellationToken);
        return found ? Results.Ok(new { id, status = "Manual" }) : Results.NotFound();
    }

    private static IResult GetConfig(IActiveEnvironmentProvider environmentProvider)
    {
        var active = environmentProvider.GetActiveEnvironment();
        var all = Enum.GetValues<D365Environment>().Select(env =>
        {
            var settings = environmentProvider.GetSettings(env);
            return new
            {
                Environment = env.ToString(),
                settings.BaseUrl,
                settings.Tenant,
                settings.CompanyId,
                settings.ClientId,
                settings.HasClientSecret,
                IsActive = env == active,
            };
        });

        return Results.Ok(new { ActiveEnvironment = active.ToString(), Environments = all });
    }

    private static IResult GetTokenStatus(ITokenService tokenService)
    {
        var statuses = Enum.GetValues<D365Environment>().Select(tokenService.GetStatus);
        return Results.Ok(statuses);
    }

    private static IResult PutConfig(PutConfigRequest request, IActiveEnvironmentProvider environmentProvider)
    {
        if (!Enum.TryParse<D365Environment>(request.ActiveEnvironment, ignoreCase: true, out var environment))
        {
            return Results.BadRequest(new { error = $"Unknown environment '{request.ActiveEnvironment}'. Expected Dev/Test/Prod." });
        }

        environmentProvider.SetActiveEnvironment(environment);
        return Results.Ok(new { ActiveEnvironment = environment.ToString() });
    }
}

/// <summary>
/// Body for PUT /api/admin/config. Intentionally minimal: only the active environment is
/// switchable through this endpoint. BaseUrl/ClientId/ClientSecret changes require editing
/// configuration (appsettings/user-secrets/env-vars) and a restart -- accepting secrets
/// over an HTTP PUT would violate AGENT_INSTRUCTION.md section 7.
/// </summary>
public sealed class PutConfigRequest
{
    public required string ActiveEnvironment { get; init; }
}
