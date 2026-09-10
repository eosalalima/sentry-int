using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SentryIntegrated.Application.Gate;
using SentryIntegrated.Application.Messaging;
using SentryIntegrated.Application.Personnel;
using SentryIntegrated.Application.Turnstile;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;
using SentryIntegrated.Infrastructure.Persistence;

namespace SentryIntegrated.Infrastructure.Polling;

public sealed class WorkerStatus
{
    private long _lastSuccessfulPollTicks;
    public DateTimeOffset? LastSuccessfulPoll => Interlocked.Read(ref _lastSuccessfulPollTicks) is var value && value > 0 ? new(value, TimeSpan.Zero) : null;
    public void MarkSuccessful() => Interlocked.Exchange(ref _lastSuccessfulPollTicks, DateTimeOffset.UtcNow.UtcTicks);
}
public sealed class TurnstilePollingWorker(IDbContextFactory<SentryDbContext> factory, IPersonnelResolver personnel,
    IPhotoResolver photos, DashboardState dashboard, SmsTemplateService templates, ISmsQueue sms,
    WorkerStatus status, IOptions<SentryOptions> options, ILogger<TurnstilePollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Turnstile polling started");
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.Value.Polling.IntervalMilliseconds));
        do { await PollAsync(stoppingToken); dashboard.Tick(); }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    internal async Task PollAsync(CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                var watermark = await db.ProcessingWatermarks.SingleOrDefaultAsync(x => x.Worker == "turnstile", ct);
                watermark ??= new ProcessingWatermark { Worker = "turnstile", LastId = 0 };
                if (db.Entry(watermark).State == EntityState.Detached) db.Add(watermark);
                var rows = await db.DeviceLogs.AsNoTracking().Where(x => x.Id > watermark.LastId)
                    .OrderBy(x => x.Id).Take(options.Value.Polling.BatchSize).ToListAsync(ct);
                foreach (var row in rows)
                {
                    var person = await personnel.ResolveAsync(row.AccessNumber, ct);
                    var device = await db.Devices.AsNoTracking().Where(x => x.Id == row.DeviceId).Select(x => x.Name).SingleOrDefaultAsync(ct) ?? $"Device {row.DeviceId}";
                    var mapped = new TurnstileEvent(row.Id, row.AccessNumber, person.DisplayName, device, row.VerifyMode, row.EventName, row.EventAddress, row.TimeLogStamp, photos.Resolve(person.PhotoName), person.PhoneNumber);
                    if (dashboard.Add(mapped) && templates.Create(mapped) is { } message && !sms.Enqueue(message)) logger.LogWarning("SMS queue full; event {EventId} was not queued", row.Id);
                    watermark.LastId = row.Id;
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Processed device event {EventId}", row.Id);
                }
                dashboard.SetDatabaseAvailable(true); status.MarkSuccessful(); return;
            }
            catch (Exception ex) when ((ex is DbUpdateException or InvalidOperationException) && !ct.IsCancellationRequested)
            {
                dashboard.SetDatabaseAvailable(false);
                if (attempt >= options.Value.Polling.RetryCount) { logger.LogError(ex, "Polling cycle failed after retries"); return; }
                await Task.Delay(TimeSpan.FromMilliseconds(200 * (1 << attempt)), ct);
            }
        }
    }
}

public sealed class DemoEventWorker(IDbContextFactory<SentryDbContext> sentryFactory, IDbContextFactory<StaffDbContext> staffFactory,
    IDbContextFactory<StudentDbContext> studentFactory, IGateService gate, IOptions<SentryOptions> options, ILogger<DemoEventWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Mode != OperatingMode.Demo) return;
        logger.LogWarning("Demo mode is active; simulated rows will be inserted into DeviceLogs");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.Gate.DemoIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await InsertDemoAsync(stoppingToken);
    }
    internal async Task InsertDemoAsync(CancellationToken ct)
    {
        string? access;
        await using (var db = await staffFactory.CreateDbContextAsync(ct)) access = await db.Staff.AsNoTracking().Select(x => x.AccessNumber).FirstOrDefaultAsync(ct);
        if (access is null) { await using var db = await studentFactory.CreateDbContextAsync(ct); access = await db.Students.AsNoTracking().Select(x => x.AccessNumber).FirstOrDefaultAsync(ct); }
        if (access is null) { logger.LogWarning("Demo event skipped because no personnel exists"); return; }
        await using var sentry = await sentryFactory.CreateDbContextAsync(ct);
        var deviceId = await sentry.Devices.AsNoTracking().Select(x => x.Id).FirstOrDefaultAsync(ct);
        if (deviceId == 0) { logger.LogWarning("Demo event skipped because no device exists"); return; }
        await gate.InsertAsync(new(access, deviceId, "Demo", "Access granted", "Integrated demo", DateTimeOffset.UtcNow), ct);
    }
}
