namespace TelemetryPlatform.Domain;

public sealed record MachinePollTracker(string MachineId, DateTimeOffset LastPolledAt, DateTimeOffset UpdatedAt);
