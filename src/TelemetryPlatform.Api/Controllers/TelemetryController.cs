using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TelemetryPlatform.Application;

namespace TelemetryPlatform.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly Func<IUnitOfWork> _unitOfWork;
    public TelemetryController(Func<IUnitOfWork> unitOfWork) => _unitOfWork = unitOfWork;

    [HttpGet("{machineId}/latest")]
    public async Task<IActionResult> GetLatest(string machineId, CancellationToken cancellationToken)
    {
        await using var uow = _unitOfWork();
        var reading = await uow.Telemetry.GetLatestAsync(machineId, cancellationToken);
        if (reading is null) return new NotFoundResult();
        return new JsonResult(new TelemetryReadingDto(reading.MachineId, reading.ObservedAt,
            JsonSerializer.Deserialize<JsonElement>(reading.Payload)));
    }

    [HttpGet("latest")]
    public async Task<IReadOnlyList<TelemetryReadingDto>> GetLatestAll([FromServices] IOptions<TelemetryOptions> options, CancellationToken cancellationToken)
    {
        await using var uow = _unitOfWork();
        var readings = await uow.Telemetry.GetLatestAsync(options.Value.Machines.Keys.ToArray(), cancellationToken);
        return readings.Select(r => new TelemetryReadingDto(r.MachineId, r.ObservedAt,
            JsonSerializer.Deserialize<JsonElement>(r.Payload))).ToArray();
    }

    [HttpPost("{machineId}")]
    public async Task<ActionResult<TelemetryReadingDto>> Ingest(string machineId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        await using var uow = _unitOfWork();
        await uow.BeginAsync(cancellationToken);
        try
        {
            var observedAt = DateTimeOffset.UtcNow;
            var reading = await uow.Telemetry.UpsertAsync(machineId, observedAt, payload.GetRawText(), cancellationToken);
            await uow.CommitAsync(cancellationToken);
            return new TelemetryReadingDto(reading!.MachineId, reading.ObservedAt, payload);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
