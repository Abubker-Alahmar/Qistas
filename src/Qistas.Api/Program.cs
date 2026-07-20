using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Qistas.Api.Endpoints;
using Qistas.Api.Middlewares;
using Qistas.Api.Swagger;
using Qistas.Infrastructure;
using Qistas.Infrastructure.Persistence;
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

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

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

    // Apply EF Core migrations automatically on startup. Chosen deliberately for this app's
    // deployment model: a single internal service (not scaled out to multiple instances),
    // reached only from the Balance WinForms app / operators on localhost -- there's no risk
    // of two instances racing to apply the same migration concurrently, and it removes the
    // manual "dotnet ef database update" step from deployment. Left un-caught on purpose:
    // Database.Migrate() throwing on a real schema problem should fail startup fast rather
    // than let the API come up against a database it can't actually use -- Serilog's
    // UseSerilog() sink still captures Log.Fatal in the catch block below either way.
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<QistasDbContext>().Database.Migrate();
    }

    // Exception middleware is outermost so it can catch anything thrown by
    // LoggingMiddleware, Serilog's request logging, or any endpoint/handler below it.
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseMiddleware<LoggingMiddleware>();

    app.UseSerilogRequestLogging();

    // Swagger is intentionally always available (not gated behind IsDevelopment()):
    // this API is an internal integration service reached only from the Balance
    // WinForms app / operators on localhost, not a public-facing production API, so
    // there's no exposure risk in leaving the docs UI reachable regardless of how
    // ASPNETCORE_ENVIRONMENT is set when the process is launched (e.g. running the
    // published exe/dll directly, which defaults to Production and previously hid
    // Swagger entirely).
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
