namespace TelemetryPlatform.Domain;

public sealed record TelemetryReading(long Id, string MachineId, DateTimeOffset ObservedAt, string Payload, DateTimeOffset CreatedAt);
