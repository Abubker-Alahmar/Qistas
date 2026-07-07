using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Qistas.Domain.Models;
using Qistas.Infrastructure.Auth;
using Qistas.Infrastructure.Options;
using Qistas.Tests.Fakes;
using Xunit;

namespace Qistas.Tests.Auth;

/// <summary>
/// Exercises <see cref="AzureAdTokenService"/> against a fake HTTP handler standing in for
/// the Azure AD token endpoint. Covers: caching within validity, proactive refresh inside
/// the 5-minute window, forced refresh via InvalidateToken, and per-environment cache
/// isolation (AGENT_INSTRUCTION.md section 4).
/// </summary>
public class AzureAdTokenServiceTests
{
    private static AzureAdTokenService CreateService(FakeTokenHttpHandler handler, QistasOptions options)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://login.microsoftonline.com/") };
        var factory = new SingleClientFactory(httpClient);
        var optionsMonitor = new TestOptionsMonitor<QistasOptions>(options);
        var secretProtector = new IdentitySecretProtector();
        var logger = NullLogger<AzureAdTokenService>.Instance;

        return new AzureAdTokenService(factory, optionsMonitor, secretProtector, logger);
    }

    private static QistasOptions BuildOptions() => new()
    {
        Tenant = "AlsahlGroup.com",
        Environments = new Dictionary<string, D365EnvironmentOptions>
        {
            ["Dev"] = new D365EnvironmentOptions
            {
                BaseUrl = "https://btodevbox.axcloud.dynamics.com/",
                CompanyId = "Bell",
                ClientId = "dev-client-id",
                ClientSecretProtected = "dev-secret",
            },
            ["Test"] = new D365EnvironmentOptions
            {
                BaseUrl = "https://alsahl-test.sandbox.operations.eu.dynamics.com/",
                CompanyId = "Bell",
                ClientId = "test-client-id",
                ClientSecretProtected = "test-secret",
            },
        },
    };

    [Fact]
    public async Task GetAccessTokenAsync_SecondCallWithinValidity_DoesNotHitTokenEndpointAgain()
    {
        var handler = new FakeTokenHttpHandler { ExpiresInSeconds = "3599" };
        var service = CreateService(handler, BuildOptions());

        string first = await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);
        string second = await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_TokenWithinFiveMinuteWindow_TriggersProactiveRefresh()
    {
        // expires_in below the 5-minute (300s) proactive refresh window -- the cached token
        // is considered due for refresh on the very next call.
        var handler = new FakeTokenHttpHandler { ExpiresInSeconds = "60" };
        var service = CreateService(handler, BuildOptions());

        await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);
        Assert.Equal(1, handler.CallCount);

        await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task InvalidateToken_ForcesReFetchOnNextCall()
    {
        var handler = new FakeTokenHttpHandler { ExpiresInSeconds = "3599" };
        var service = CreateService(handler, BuildOptions());

        await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);
        Assert.Equal(1, handler.CallCount);

        service.InvalidateToken(D365Environment.Dev);
        await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_PerEnvironmentCacheIsolation_DevTokenNeverReturnedForTest()
    {
        var handler = new FakeTokenHttpHandler { ExpiresInSeconds = "3599" };
        var service = CreateService(handler, BuildOptions());

        string devToken = await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);
        string testToken = await service.GetAccessTokenAsync(D365Environment.Test, CancellationToken.None);

        Assert.NotEqual(devToken, testToken);
        Assert.Equal("token-for-dev-client-id", devToken);
        Assert.Equal("token-for-test-client-id", testToken);
        Assert.Equal(2, handler.CallCount);

        // Fetching Dev again must still return the Dev-cached token, not Test's.
        string devTokenAgain = await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);
        Assert.Equal(devToken, devTokenAgain);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public void GetStatus_NoTokenFetchedYet_ReportsNoToken()
    {
        var handler = new FakeTokenHttpHandler();
        var service = CreateService(handler, BuildOptions());

        var status = service.GetStatus(D365Environment.Dev);

        Assert.False(status.HasToken);
        Assert.True(status.IsExpired);
    }

    [Fact]
    public async Task GetStatus_AfterFetch_ReportsHasTokenAndExpiry()
    {
        var handler = new FakeTokenHttpHandler { ExpiresInSeconds = "3599" };
        var service = CreateService(handler, BuildOptions());

        await service.GetAccessTokenAsync(D365Environment.Dev, CancellationToken.None);
        var status = service.GetStatus(D365Environment.Dev);

        Assert.True(status.HasToken);
        Assert.False(status.IsExpired);
        Assert.NotNull(status.ExpiresAtUtc);
    }
}
