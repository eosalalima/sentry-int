using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;
using SentryIntegrated.Infrastructure.Persistence;

namespace SentryIntegrated.Application.Messaging;

public interface ISmsSender { Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken); }
public interface ISmsQueue { bool Enqueue(SmsMessage message); }
public sealed class SmsQueue : ISmsQueue
{
    internal Channel<SmsMessage> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<SmsMessage>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    public bool Enqueue(SmsMessage message) => Channel.Writer.TryWrite(message);
}
public static partial class PhoneNumbers
{
    [GeneratedRegex("[^0-9+]")] private static partial Regex InvalidCharacters();
    public static string? Normalize(string? input, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = InvalidCharacters().Replace(input, "");
        if (value.StartsWith("00")) value = "+" + value[2..];
        else if (value.StartsWith('0')) value = countryCode + value[1..];
        if (!value.StartsWith('+') || value.Length is < 9 or > 16 || value[1..].Any(c => !char.IsDigit(c))) return null;
        return value;
    }
}
public sealed class SmsTemplateService(IOptions<SentryOptions> options)
{
    public SmsMessage? Create(TurnstileEvent e)
    {
        var number = PhoneNumbers.Normalize(e.PhoneNumber, options.Value.Sms.CountryCode);
        return number is null ? null : new(e.Id, number, $"Sentry: {e.PersonName} recorded {e.EventName} at {e.Timestamp:yyyy-MM-dd HH:mm}.");
    }
}
public sealed class DisabledSmsSender : ISmsSender
{
    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken) => Task.FromResult(SmsSendResult.Failure("SMS provider is disabled."));
}
public sealed class RecordingSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(long EventId, DateTimeOffset At)> _sent = new();
    public IReadOnlyCollection<(long EventId, DateTimeOffset At)> Sent => _sent.ToArray();
    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken) { _sent.Enqueue((message.EventId, DateTimeOffset.UtcNow)); return Task.FromResult(SmsSendResult.Success("recorded")); }
}
public sealed class GsmModemSmsSender(IOptions<SentryOptions> options, ILogger<GsmModemSmsSender> logger) : ISmsSender
{
    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct)
    {
        var settings = options.Value.Sms.Gsm;
        if (string.IsNullOrWhiteSpace(settings.PortName)) return SmsSendResult.Failure("GSM port is not configured.");
        try
        {
            using var port = new SerialPort(settings.PortName, settings.BaudRate) { NewLine = "\r", ReadTimeout = settings.TimeoutSeconds * 1000, WriteTimeout = settings.TimeoutSeconds * 1000 };
            port.Open();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            await WriteAsync(port, "AT\r", timeout.Token);
            await WriteAsync(port, "AT+CMGF=1\r", timeout.Token);
            await WriteAsync(port, $"AT+CMGS=\"{message.Destination}\"\r", timeout.Token);
            await WriteAsync(port, message.Body + (char)26, timeout.Token);
            logger.LogInformation("GSM accepted SMS for event {EventId}", message.EventId);
            return SmsSendResult.Success("modem-accepted");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or OperationCanceledException or InvalidOperationException)
        {
            logger.LogWarning("GSM send failed for event {EventId}: {ErrorType}", message.EventId, ex.GetType().Name);
            return SmsSendResult.Failure(ex.GetType().Name);
        }
    }
    private static async Task WriteAsync(SerialPort port, string command, CancellationToken ct)
    { var bytes = Encoding.ASCII.GetBytes(command); await port.BaseStream.WriteAsync(bytes, ct); await port.BaseStream.FlushAsync(ct); }
}

public sealed class SmsWorker(SmsQueue queue, ISmsSender sender, IDbContextFactory<SentryDbContext> factory, IOptions<SentryOptions> options, ILogger<SmsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Channel.Reader.ReadAllAsync(stoppingToken))
        {
            await using var db = await factory.CreateDbContextAsync(stoppingToken);
            var delivery = await db.SmsDeliveries.SingleOrDefaultAsync(x => x.EventId == message.EventId, stoppingToken);
            if (delivery?.Delivered == true || (delivery?.Attempts ?? 0) >= options.Value.Sms.MaximumAttempts) continue;
            delivery ??= new SmsDelivery { EventId = message.EventId }; if (delivery.Attempts == 0) db.SmsDeliveries.Add(delivery);
            for (; delivery.Attempts < options.Value.Sms.MaximumAttempts && !delivery.Delivered;)
            {
                delivery.Attempts++; var result = await sender.SendAsync(message, stoppingToken); delivery.Delivered = result.Delivered; delivery.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(stoppingToken);
                if (!result.Delivered && delivery.Attempts < options.Value.Sms.MaximumAttempts) await Task.Delay(options.Value.Sms.RetryDelayMilliseconds, stoppingToken);
            }
            logger.LogInformation("SMS event {EventId} completed after {Attempts} attempts; delivered={Delivered}", message.EventId, delivery.Attempts, delivery.Delivered);
        }
    }
}
