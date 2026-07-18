using Microsoft.OpenApi.Models;
using Qistas.Application.Abstractions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Qistas.Api.Swagger;

/// <summary>
/// Stamps the currently active D365 environment (Dev/Test/Prod) into both Swagger
/// document descriptions, so it's impossible to miss which environment a developer is
/// pointed at while exploring the API (Balance/CLAUDE.md #16.20 -- guards against a
/// Dev/Prod environment mix-up).
/// </summary>
public sealed class SwaggerActiveEnvironmentDocumentFilter : IDocumentFilter
{
    private readonly IActiveEnvironmentProvider _environmentProvider;

    public SwaggerActiveEnvironmentDocumentFilter(IActiveEnvironmentProvider environmentProvider)
    {
        _environmentProvider = environmentProvider;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var environment = _environmentProvider.GetActiveEnvironment();
        string banner = $"\n\n> **Active D365 environment: {environment}**";

        if (swaggerDoc.Info is not null)
        {
            swaggerDoc.Info.Description = (swaggerDoc.Info.Description ?? string.Empty) + banner;
        }
    }
}
