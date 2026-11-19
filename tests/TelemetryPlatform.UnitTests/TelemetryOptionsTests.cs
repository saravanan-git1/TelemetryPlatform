using TelemetryPlatform.Application;
using TelemetryPlatform.Api.Hubs;

namespace TelemetryPlatform.UnitTests;

public sealed class TelemetryOptionsTests
{
    [Fact]
    public void Machine_source_paths_are_configurable_per_machine()
    {
        var options = new TelemetryOptions
        {
            Machines = new Dictionary<string, string>
            {
                ["machine-a"] = "/api/v1/machine-a",
                ["machine-b"] = "/api/v1/machine-b"
            }
        };

        Assert.Equal("/api/v1/machine-a", options.Machines["machine-a"]);
        Assert.Equal(2, options.Machines.Count);
    }

    [Fact]
    public void SignalR_group_uses_machine_id()
    {
        Assert.Equal("machine:machine-a", TelemetryHub.GroupName("machine-a"));
    }
}
