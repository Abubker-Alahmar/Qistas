using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qistas.Application.Abstractions;
using Qistas.Domain.Contracts;
using Qistas.Domain.Json;
using Qistas.Domain.Models;
using Qistas.Infrastructure.Logging;

namespace Qistas.Infrastructure.D365;

/// <summary>
/// Typed HttpClient for the BTOLoadIntService custom service operations. Transient
/// network failures / 5xx / timeouts are handled by the resilience pipeline attached to
/// the HttpClient itself in ServiceCollectionExtensions (exponential backoff + circuit
/// breaker + per-attempt timeout, all async -- never blocking the caller,
/// AGENT_INSTRUCTION.md section 5). This class additionally owns the 401 handling: on a
/// 401 it invalidates the cached token, acquires a fresh one, and retries the call
/// exactly once (AGENT_INSTRUCTION.md section 4) before giving up.
/// </summary>
public sealed class D365Client(
    HttpClient httpClient,
    ITokenService tokenService,
    IActiveEnvironmentProvider environmentProvider,
    ILogger<D365Client> logger,
    Application.Logging.IIntegrationLogRepository integrationLog)
    : ID365Client
{
    private const string EntryOperation = "setEntryWeightDetails";
    private const string GetLoadOperation = "getLoadDetails";
    private const string ExitOperation = "setExitWeightDetails";

    public async Task<D365CallResult<D365Response>> SetEntryWeightDetailsAsync(
        SetEntryWeightDetailsRequest request, D365Environment environment, CancellationToken cancellationToken)
        => await SendAsync(EntryOperation, request, environment, cancellationToken);

    public async Task<D365CallResult<D365Response>> GetLoadDetailsAsync(
        GetLoadDetailsRequest request, D365Environment environment, CancellationToken cancellationToken)
        => await SendAsync(GetLoadOperation, request, environment, cancellationToken);

    public async Task<D365CallResult<D365Response>> SetExitWeightDetailsAsync(
        SetExitWeightDetailsRequest request, D365Environment environment, CancellationToken cancellationToken)
        => await SendAsync(ExitOperation, request, environment, cancellationToken);

    private async Task<D365CallResult<D365Response>> SendAsync<TRequest>(
        string operation,
        TRequest request,
        D365Environment environment,
        CancellationToken cancellationToken)
    {
        // Every operation shares the same envelope: a single top-level "_request" property
        // (APIs V2.0 Common Request Scheme) and the flat common response (D365Response).
        var envelope = new D365RequestEnvelope<TRequest> { Request = request };
        var settings = environmentProvider.GetSettings(environment);
        string url = CombineUrl(settings.BaseUrl, operation);
        string requestJson = JsonSerializer.Serialize(envelope, QistasJson.Options);

        string redactedRequest = SecretRedactor.Redact(requestJson);
        logger.LogInformation(
            "D365 {Operation} request ({Environment}): {RequestBody}",
            operation, environment, redactedRequest);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var (httpResponse, body) = await SendWithAuthRetryAsync(url, requestJson, environment, cancellationToken);
            using var _ = httpResponse; // body already read; dispose deterministically
            stopwatch.Stop();

            string redactedResponse = SecretRedactor.Redact(body);
            logger.LogInformation(
                "D365 {Operation} response ({Environment}, {StatusCode}): {ResponseBody}",
                operation, environment, (int)httpResponse.StatusCode, redactedResponse);

            if (!httpResponse.IsSuccessStatusCode)
            {
                await WriteDbLogAsync(environment, operation, success: false, (int)httpResponse.StatusCode,
                    redactedRequest, redactedResponse, $"HTTP {(int)httpResponse.StatusCode}", stopwatch.ElapsedMilliseconds, cancellationToken);
                return D365CallResult<D365Response>.TransportFailure(
                    $"D365 {operation} returned HTTP {(int)httpResponse.StatusCode}.");
            }

            var response = JsonSerializer.Deserialize<D365Response>(body, QistasJson.Options);
            if (response is null)
            {
                await WriteDbLogAsync(environment, operation, success: false, (int)httpResponse.StatusCode,
                    redactedRequest, redactedResponse, "Empty/unparseable response", stopwatch.ElapsedMilliseconds, cancellationToken);
                return D365CallResult<D365Response>.TransportFailure($"Empty/unparseable response for {operation}.");
            }

            await WriteDbLogAsync(environment, operation, success: response.Status, (int)httpResponse.StatusCode,
                redactedRequest, redactedResponse, response.Status ? null : response.Message,
                stopwatch.ElapsedMilliseconds, cancellationToken);
            return D365CallResult<D365Response>.Ok(response, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Reached only after the resilience pipeline (Polly inline retry per the
            // configured attempts/backoff + circuit breaker) has been exhausted -- the
            // caller (Application use case) then ARCHIVES the message in the database for
            // manual employee action; there is no automatic background re-sender
            // (AGENT_INSTRUCTION.md section 5).
            logger.LogError(ex, "D365 {Operation} failed for environment {Environment} after resilience pipeline exhaustion.", operation, environment);
            await WriteDbLogAsync(environment, operation, success: false, httpStatusCode: null,
                redactedRequest, responseJson: null, ex.Message, stopwatch.ElapsedMilliseconds, cancellationToken);
            return D365CallResult<D365Response>.TransportFailure(ex.Message);
        }
    }

    /// <summary>
    /// Persists one log row per call attempt to the database (owner requirement: logs in
    /// DB, not only files). Logging must never break the actual call -- failures here are
    /// swallowed after being written to the file log.
    /// </summary>
    private async Task WriteDbLogAsync(
        D365Environment environment, string operation, bool success, int? httpStatusCode,
        string? requestJson, string? responseJson, string? error, long durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await integrationLog.AddAsync(new Application.Logging.IntegrationLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Environment = environment.ToString(),
                Operation = operation,
                Success = success,
                HttpStatusCode = httpStatusCode,
                RequestJson = requestJson,
                ResponseJson = responseJson,
                Error = error,
                DurationMs = durationMs,
            }, cancellationToken);
        }
        catch (Exception logEx)
        {
            logger.LogError(logEx, "Failed to write integration log row for {Operation}.", operation);
        }
    }

    private async Task<(HttpResponseMessage response, string body)> SendWithAuthRetryAsync(
        string url, string requestJson, D365Environment environment, CancellationToken cancellationToken)
    {
        var token = await tokenService.GetAccessTokenAsync(environment, cancellationToken);
        var (response, body) = await PostAsync(url, requestJson, token, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return (response, body);
        }

        // 401: refresh once and retry exactly once (AGENT_INSTRUCTION.md section 4).
        logger.LogWarning("D365 call to {Url} returned 401 -- invalidating cached token and retrying once.", url);
        tokenService.InvalidateToken(environment);
        response.Dispose();

        string freshToken = await tokenService.GetAccessTokenAsync(environment, cancellationToken);
        return await PostAsync(url, requestJson, freshToken, cancellationToken);
    }

    private async Task<(HttpResponseMessage response, string body)> PostAsync(
        string url, string requestJson, string bearerToken, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response, body);
    }

    private static string CombineUrl(string baseUrl, string operation)
    {
        string normalizedBase = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
        return $"{normalizedBase}api/services/BTOLoadIntServiceGroup/BTOLoadIntService/{operation}";
    }
}
