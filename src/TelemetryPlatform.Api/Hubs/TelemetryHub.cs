using Microsoft.AspNetCore.SignalR;

namespace TelemetryPlatform.Api.Hubs;

public sealed class TelemetryHub : Hub
{
    public Task Subscribe(string machineId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(machineId));

    public Task Unsubscribe(string machineId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(machineId));

    public static string GroupName(string machineId) => $"machine:{machineId}";
}
