using System.ComponentModel.DataAnnotations;

namespace SentryIntegrated.Configuration;

public enum OperatingMode { Live, Demo }
public enum SmsProviderKind { Disabled, Recording, GsmModem }

public sealed class SentryOptions
{
    public const string SectionName = "Sentry";
    public OperatingMode Mode { get; set; } = OperatingMode.Live;
    public PollingOptions Polling { get; set; } = new();
    public DashboardOptions Dashboard { get; set; } = new();
    public PhotoOptions Photos { get; set; } = new();
    public GateOptions Gate { get; set; } = new();
    public SmsOptions Sms { get; set; } = new();
}
public sealed class PollingOptions
{
    [Range(100, 60000)] public int IntervalMilliseconds { get; set; } = 1000;
    [Range(1, 1000)] public int BatchSize { get; set; } = 100;
    [Range(0, 10)] public int RetryCount { get; set; } = 3;
}
public sealed class DashboardOptions
{
    [Range(100, 300000)] public int SpotlightMilliseconds { get; set; } = 5000;
    [Range(1, 1000)] public int FeedLimit { get; set; } = 50;
    [Range(1, 5000)] public int QueueLimit { get; set; } = 500;
}
public sealed class PhotoOptions
{
    public string BaseUrl { get; set; } = "/photos";
    public string FallbackUrl { get; set; } = "/images/person-fallback.svg";
}
public sealed class GateOptions
{
    [Range(1, 100)] public int MaximumAccessNumberLength { get; set; } = 30;
    public int DemoIntervalSeconds { get; set; } = 30;
}
public sealed class SmsOptions
{
    public SmsProviderKind Provider { get; set; } = SmsProviderKind.Disabled;
    [Range(0, 10)] public int MaximumAttempts { get; set; } = 3;
    [Range(50, 60000)] public int RetryDelayMilliseconds { get; set; } = 1000;
    public string CountryCode { get; set; } = "+63";
    public GsmOptions Gsm { get; set; } = new();
}
public sealed class GsmOptions
{
    public string? PortName { get; set; }
    [Range(1200, 921600)] public int BaudRate { get; set; } = 9600;
    [Range(1, 120)] public int TimeoutSeconds { get; set; } = 15;
}
