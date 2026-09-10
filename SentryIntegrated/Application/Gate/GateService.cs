using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;
using SentryIntegrated.Infrastructure.Persistence;

namespace SentryIntegrated.Application.Gate;

public sealed class GateValidationException(string message) : ValidationException(message);
public interface IGateService { Task<long> InsertAsync(GateEventRequest request, CancellationToken cancellationToken); }
public sealed class GateService(IDbContextFactory<SentryDbContext> factory, IOptions<SentryOptions> options, ILogger<GateService> logger) : IGateService
{
    public async Task<long> InsertAsync(GateEventRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.AccessNumber) || r.AccessNumber.Length > options.Value.Gate.MaximumAccessNumberLength) throw new GateValidationException("A valid access number is required.");
        if (r.DeviceId <= 0) throw new GateValidationException("A valid device is required.");
        if (string.IsNullOrWhiteSpace(r.EventName)) throw new GateValidationException("An event name is required.");
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Devices.AnyAsync(x => x.Id == r.DeviceId, ct)) throw new GateValidationException("The selected device does not exist.");
        var row = new DeviceLog { AccessNumber = r.AccessNumber.Trim(), DeviceId = r.DeviceId, VerifyMode = r.VerifyMode.Trim(), EventName = r.EventName.Trim(), EventAddress = r.EventAddress.Trim(), TimeLogStamp = r.Timestamp ?? DateTimeOffset.UtcNow };
        db.DeviceLogs.Add(row); await db.SaveChangesAsync(ct);
        logger.LogInformation("Gate event {EventId} inserted for device {DeviceId}", row.Id, row.DeviceId);
        return row.Id;
    }
}
