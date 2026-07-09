using Microsoft.OpenApi.Models;
using Qistas.Api.Endpoints;
using Qistas.Api.Swagger;
using Qistas.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddQistasInfrastructure(builder.Configuration);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("balance", new OpenApiInfo
        {
            Title = "Balance API",
            Version = "v1",
            Description = "Endpoints called by the Balance WinForms weighbridge app over localhost HTTP " +
                          "(Weight-In, getLoadDetails, Weight-Out, health).",
        });

        options.SwaggerDoc("admin", new OpenApiInfo
        {
            Title = "Qistas Developer API",
            Version = "v1",
            Description = "Outbox review/retry, configuration snapshot, and token status for operators and developers.",
        });

        options.DocInclusionPredicate((documentName, apiDescription) =>
            SwaggerGroupDocumentFilter.BelongsToDocument(documentName, apiDescription));

        options.DocumentFilter<SwaggerActiveEnvironmentDocumentFilter>();
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/balance/swagger.json", "Balance API");
        options.SwaggerEndpoint("/swagger/admin/swagger.json", "Qistas Developer API");
        options.DocumentTitle = "Qistas Integration API";
    });

    app.MapBalanceEndpoints();
    app.MapAdminEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Qistas.Api terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
