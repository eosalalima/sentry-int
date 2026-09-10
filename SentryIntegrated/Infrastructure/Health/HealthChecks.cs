using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SentryIntegrated.Infrastructure.Persistence;
using SentryIntegrated.Infrastructure.Polling;
using Microsoft.Extensions.Options;
using SentryIntegrated.Configuration;

namespace SentryIntegrated.Infrastructure.Health;

public sealed class DatabaseReadinessCheck(IDbContextFactory<SentryDbContext> sentry,
    IDbContextFactory<StaffDbContext> staff, IDbContextFactory<StudentDbContext> students) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var a = await sentry.CreateDbContextAsync(ct); await a.Database.CanConnectAsync(ct);
            await using var b = await staff.CreateDbContextAsync(ct); await b.Database.CanConnectAsync(ct);
            await using var c = await students.CreateDbContextAsync(ct); await c.Database.CanConnectAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch { return HealthCheckResult.Unhealthy("A required database is unavailable."); }
    }
}
public sealed class PollingReadinessCheck(WorkerStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(status.LastSuccessfulPoll is not null && DateTimeOffset.UtcNow - status.LastSuccessfulPoll < TimeSpan.FromMinutes(2)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Polling has not completed recently."));
}
public sealed class SmsReadinessCheck(IOptions<SentryOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var sms = options.Value.Sms;
        var healthy = sms.Provider != SmsProviderKind.GsmModem || !string.IsNullOrWhiteSpace(sms.Gsm.PortName);
        return Task.FromResult(healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("The selected SMS provider is not configured."));
    }
}
