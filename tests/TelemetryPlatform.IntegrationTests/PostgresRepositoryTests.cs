using TelemetryPlatform.Infrastructure;

namespace TelemetryPlatform.IntegrationTests;

public sealed class PostgresRepositoryTests
{
    [PostgresFact]
    public async Task Locally_configured_postgres_supports_schema_and_reading_round_trip()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings__Postgres must be set.");

        await new PostgresSchemaInitializer(connectionString).InitializeAsync();
        await using var uow = new PostgresUnitOfWork(connectionString);
        await uow.BeginAsync();
        var observedAt = DateTimeOffset.UtcNow;
        var reading = await uow.Telemetry.UpsertAsync("integration-machine", observedAt,
            """{"temperature":21.5}""");
        await uow.PollTracker.UpdateAsync("integration-machine", observedAt);
        await uow.CommitAsync();

        Assert.NotNull(reading);
        Assert.Equal("integration-machine", reading!.MachineId);
        Assert.Equal(observedAt, await uow.PollTracker.GetLastPolledAtAsync("integration-machine"));
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")))
            Skip = "Set ConnectionStrings__Postgres to run the PostgreSQL integration test.";
    }
}
