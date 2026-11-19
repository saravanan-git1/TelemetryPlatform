namespace TelemetryPlatform.Application;

public sealed record TelemetryReadingDto(string MachineId, DateTimeOffset ObservedAt, object Payload);
