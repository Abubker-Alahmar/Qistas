using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Qistas.Application.Abstractions;
using Qistas.Application.Outbox;
using Qistas.Application.UseCases;
using Qistas.Infrastructure.Auth;
using Qistas.Infrastructure.D365;
using Qistas.Infrastructure.Options;
using Qistas.Infrastructure.Outbox;
using Qistas.Infrastructure.Persistence;
using Qistas.Infrastructure.Secrets;

namespace Qistas.Infrastructure;

/// <summary>
/// Wires up every Infrastructure + Application service: options binding, the resilience
/// pipeline (Polly v8 inline retry/circuit-breaker/timeout, PLAN.md 1.3), the token
/// service, the D365 typed client, the SQL Server failed-message archive + integration log,
/// the DPAPI/no-op secret protector, and the Application-layer use case handlers.
/// Retry model (owner decision): Polly retries INLINE per configuration while the truck
/// is on the scale; on exhaustion the message is archived in the database for MANUAL
/// employee action via the review screen -- there is no automatic background re-sender.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQistasInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<QistasOptions>(configuration.GetSection(QistasOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IActiveEnvironmentProvider, ActiveEnvironmentProvider>();
        services.AddSingleton<ITokenService, AzureAdTokenService>();

        // EF Core DbContext for the Outbox + IntegrationLog tables (QistasLogDb connection
        // string). AddDbContext registers as Scoped, which is required: DbContext is not
        // thread-safe and must never be captured by a singleton. The two repositories below
        // are therefore Scoped too (not Singleton as before) -- every consumer
        // (SubmitEntryWeightHandler, SubmitExitWeightHandler, RetryOutboxMessageHandler,
        // MarkOutboxManualHandler, D365Client) is already registered Scoped and only used
        // within a request-handling DI scope, so this is a safe, correct lifetime change.
        services.AddDbContext<QistasDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("QistasLogDb")));

        services.AddScoped<IOutboxRepository, SqlServerOutboxRepository>();
        services.AddScoped<Application.Logging.IIntegrationLogRepository, Logging.SqlServerIntegrationLogRepository>();

        services.AddSingleton<ISecretProtector>(_ =>
            OperatingSystem.IsWindows()
                ? new DpapiSecretProtector()
                : new NoOpSecretProtector());

        // Plain client for the Azure AD token endpoint -- kept separate from the D365
        // resilience pipeline since token acquisition already has its own
        // refresh/serialize-via-SemaphoreSlim guard in AzureAdTokenService.
        services.AddHttpClient("QistasTokenClient");

        var retrySection = configuration.GetSection($"{QistasOptions.SectionName}:{nameof(QistasOptions.Retry)}");
        var retryOptions = retrySection.Get<RetryOptions>() ?? new RetryOptions();

        services.AddHttpClient<ID365Client, D365Client>()
            .AddResilienceHandler("d365-pipeline", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = retryOptions.MaxAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(retryOptions.BaseDelaySeconds),
                    UseJitter = true,
                });

                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = retryOptions.CircuitBreakerFailureRatio,
                    SamplingDuration = TimeSpan.FromSeconds(retryOptions.CircuitBreakerSamplingDurationSeconds),
                    MinimumThroughput = retryOptions.CircuitBreakerMinimumThroughput,
                    BreakDuration = TimeSpan.FromSeconds(retryOptions.CircuitBreakerBreakDurationSeconds),
                });

                builder.AddTimeout(TimeSpan.FromSeconds(retryOptions.TimeoutSeconds));
            });

        services.AddScoped<SubmitEntryWeightHandler>();
        services.AddScoped<GetLoadForValidationHandler>();
        services.AddScoped<SubmitExitWeightHandler>();
        services.AddScoped<RetryOutboxMessageHandler>();
        services.AddScoped<MarkOutboxManualHandler>();

        return services;
    }
}
