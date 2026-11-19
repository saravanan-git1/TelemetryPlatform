using Dapper;
using Npgsql;

namespace TelemetryPlatform.Infrastructure;

public sealed class PostgresSchemaInitializer
{
    private readonly string _connectionString;
    public PostgresSchemaInitializer(string connectionString) => _connectionString = connectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE IF NOT EXISTS telemetry_readings (
                id BIGSERIAL PRIMARY KEY,
                machine_id TEXT NOT NULL,
                observed_at TIMESTAMPTZ NOT NULL,
                payload JSONB NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                UNIQUE(machine_id, observed_at)
            );
            CREATE INDEX IF NOT EXISTS ix_telemetry_readings_machine_observed
                ON telemetry_readings (machine_id, observed_at DESC);
            CREATE TABLE IF NOT EXISTS machine_poll_tracker (
                machine_id TEXT PRIMARY KEY,
                last_polled_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """, cancellationToken: cancellationToken));
    }
}
