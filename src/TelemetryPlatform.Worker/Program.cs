using TelemetryPlatform.Application;
using TelemetryPlatform.Infrastructure;
using TelemetryPlatform.Worker;
using StackExchange.Redis;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<TelemetryOptions>(builder.Configuration.GetSection(TelemetryOptions.SectionName));

var postgres = builder.Configuration.GetConnectionString("Postgres") ?? "";
var redis = builder.Configuration["Redis:ConnectionString"] ?? "";
var telemetry = builder.Configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>()
    ?? throw new InvalidOperationException("Telemetry configuration is required.");

if (string.IsNullOrWhiteSpace(postgres))
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
if (string.IsNullOrWhiteSpace(redis))
    throw new InvalidOperationException("Redis:ConnectionString must be configured.");
if (!Uri.TryCreate(telemetry.SourceUrl, UriKind.Absolute, out var sourceUri) ||
    (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
    throw new InvalidOperationException("Telemetry:SourceUrl must be an absolute HTTP or HTTPS URL.");
if (telemetry.Machines.Count == 0)
    throw new InvalidOperationException("Configure at least one machine under Telemetry:Machines.");
if (telemetry.Machines.Any(machine => string.IsNullOrWhiteSpace(machine.Key) || string.IsNullOrWhiteSpace(machine.Value)))
    throw new InvalidOperationException("Every Telemetry:Machines entry must have a machine identifier and source path.");
if (telemetry.Machines.Any(machine => Uri.TryCreate(machine.Value, UriKind.Absolute, out _)))
    throw new InvalidOperationException("Telemetry:Machines source paths must be relative to Telemetry:SourceUrl.");

builder.Services.AddSingleton(sp => new PostgresSchemaInitializer(postgres));
builder.Services.AddScoped<IUnitOfWork>(sp => new PostgresUnitOfWork(postgres));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
builder.Services.AddScoped<ITelemetryNotifier, RedisTelemetryNotifier>();
builder.Services.AddSingleton<IDistributedLock, RedisDistributedLock>();
builder.Services.AddHttpClient("TelemetrySource", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<TelemetryWorker>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TelemetryPlatform.Worker"))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var host = builder.Build();
await host.Services.GetRequiredService<PostgresSchemaInitializer>().InitializeAsync();
host.Run();
