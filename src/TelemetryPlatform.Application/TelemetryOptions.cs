namespace TelemetryPlatform.Application;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int PollIntervalSeconds { get; set; } = 30;
    public string SourceUrl { get; set; } = "http://localhost";
    public string ApiKey { get; set; } = string.Empty;
    public Dictionary<string, string> Machines { get; set; } = new();
}
