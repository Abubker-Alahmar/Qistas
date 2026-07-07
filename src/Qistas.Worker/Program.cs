using Qistas.Infrastructure;
using Qistas.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddQistasInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OutboxRetryWorker>();

var host = builder.Build();
host.Run();
