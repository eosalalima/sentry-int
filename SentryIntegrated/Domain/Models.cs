namespace SentryIntegrated.Domain;

public sealed record TurnstileEvent(long Id, string AccessNumber, string PersonName,
    string DeviceName, string VerifyMode, string EventName, string EventAddress,
    DateTimeOffset Timestamp, string PhotoUrl, string? PhoneNumber);

public sealed record Person(string AccessNumber, string DisplayName, string? PhotoName,
    string? PhoneNumber, PersonKind Kind);
public enum PersonKind { Staff, Student, Unknown }

public sealed record GateEventRequest(string AccessNumber, int DeviceId, string VerifyMode,
    string EventName, string EventAddress, DateTimeOffset? Timestamp);

public sealed record SmsMessage(long EventId, string Destination, string Body);
public sealed record SmsSendResult(bool Delivered, string? ProviderReference, string? Error)
{
    public static SmsSendResult Success(string? reference = null) => new(true, reference, null);
    public static SmsSendResult Failure(string error) => new(false, null, error);
}
