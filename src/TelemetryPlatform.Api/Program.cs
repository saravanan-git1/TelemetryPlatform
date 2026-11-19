using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.HttpOverrides;
using StackExchange.Redis;
using TelemetryPlatform.Api;
using TelemetryPlatform.Api.Health;
using TelemetryPlatform.Api.Hubs;
using TelemetryPlatform.Application;
using TelemetryPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

var postgres = builder.Configuration.GetConnectionString("Postgres") ?? "";
var redis = builder.Configuration["Redis:ConnectionString"] ?? "";
if (string.IsNullOrWhiteSpace(postgres))
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
if (string.IsNullOrWhiteSpace(redis))
    throw new InvalidOperationException("Redis:ConnectionString must be configured.");

builder.Services.Configure<TelemetryOptions>(builder.Configuration.GetSection(TelemetryOptions.SectionName));
builder.Services.AddSingleton(sp => new PostgresSchemaInitializer(postgres));
builder.Services.AddScoped<IUnitOfWork>(sp => new PostgresUnitOfWork(postgres));
builder.Services.AddSingleton<Func<IUnitOfWork>>(sp => () => new PostgresUnitOfWork(postgres));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
builder.Services.AddScoped<ITelemetryNotifier, RedisTelemetryNotifier>();

var healthBuilder = builder.Services.AddHealthChecks();
healthBuilder.AddCheck("postgres", new PostgresHealthCheck(postgres), tags: ["ready"]);
healthBuilder.AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
builder.Services.AddHostedService<RedisTelemetryBroadcastService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TelemetryPlatform.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

await app.Services.GetRequiredService<PostgresSchemaInitializer>().InitializeAsync();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

app.Run();

public partial class Program;
