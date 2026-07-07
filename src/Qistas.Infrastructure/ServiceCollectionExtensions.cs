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
using Qistas.Infrastructure.Secrets;

namespace Qistas.Infrastructure;

/// <summary>
/// Wires up every Infrastructure + Application service: options binding, the resilience
/// pipeline (Polly v8 retry/circuit-breaker/timeout, PLAN.md 1.3), the token service, the
/// D365 typed client, the SQLite outbox, the DPAPI/no-op secret protector, and the
/// Application-layer use case handlers. Shared by Qistas.Api and Qistas.Worker so both
/// hosts get an identical, consistently-configured pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQistasInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<QistasOptions>(configuration.GetSection(QistasOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IActiveEnvironmentProvider, ActiveEnvironmentProvider>();
        services.AddSingleton<ITokenService, AzureAdTokenService>();
        services.AddSingleton<IOutboxRepository, SqliteOutboxRepository>();

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
