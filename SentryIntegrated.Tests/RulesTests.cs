using Microsoft.Extensions.Options;
using SentryIntegrated.Application.Messaging;
using SentryIntegrated.Application.Personnel;
using SentryIntegrated.Application.Turnstile;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;

namespace SentryIntegrated.Tests;

public sealed class RulesTests
{
    [Theory]
    [InlineData("0917 123 4567", "+639171234567")]
    [InlineData("00639171234567", "+639171234567")]
    [InlineData("invalid", null)]
    public void Phone_number_normalization(string input, string? expected) => Assert.Equal(expected, PhoneNumbers.Normalize(input, "+63"));

    [Fact]
    public void Photo_resolution_uses_safe_fallback()
    {
        var resolver = new PhotoResolver(Options.Create(new SentryOptions()));
        Assert.Equal("/images/person-fallback.svg", resolver.Resolve(null));
        Assert.Equal("/images/person-fallback.svg", resolver.Resolve("../secret"));
        Assert.Equal("/photos/person.jpg", resolver.Resolve("person.jpg"));
    }

    [Fact]
    public void Sms_message_creation_requires_valid_number()
    {
        var service = new SmsTemplateService(Options.Create(new SentryOptions()));
        Assert.Null(service.Create(Event(1) with { PhoneNumber = null }));
        var message = service.Create(Event(1) with { PhoneNumber = "09171234567" });
        Assert.Equal(1, message!.EventId); Assert.DoesNotContain("09171234567", message.Body);
    }

    [Fact]
    public async Task Disabled_provider_fails_safely() => Assert.False((await new DisabledSmsSender().SendAsync(new(1, "+639171234567", "test"), default)).Delivered);

    [Fact]
    public void Dashboard_deduplicates_and_orders_rapid_events()
    {
        var state = State(); Assert.True(state.Add(Event(1))); Assert.True(state.Add(Event(2))); Assert.True(state.Add(Event(3))); Assert.False(state.Add(Event(2)));
        Assert.Equal(1, state.Snapshot().Spotlight!.Id); Assert.Equal(new long[] { 2, 3 }, state.Snapshot().Queue.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void Dashboard_is_bounded()
    {
        var clock = new FakeTimeProvider(); var options = new SentryOptions(); options.Dashboard.SpotlightMilliseconds = 100; options.Dashboard.FeedLimit = 2;
        var state = new DashboardState(Options.Create(options), clock);
        for (var id = 1; id <= 4; id++) { state.Add(Event(id)); clock.Advance(TimeSpan.FromMilliseconds(101)); state.Tick(); }
        Assert.True(state.Snapshot().Feed.Count <= 2);
    }

    [Fact]
    public void Spotlight_transitions_queue_to_feed()
    {
        var clock = new FakeTimeProvider(); var options = new SentryOptions(); options.Dashboard.SpotlightMilliseconds = 100;
        var state = new DashboardState(Options.Create(options), clock); state.Add(Event(1)); state.Add(Event(2)); clock.Advance(TimeSpan.FromMilliseconds(101)); state.Tick();
        Assert.Equal(2, state.Snapshot().Spotlight!.Id); Assert.Equal(1, state.Snapshot().Feed.Single().Id);
    }
    private static DashboardState State() => new(Options.Create(new SentryOptions()), TimeProvider.System);
    private static TurnstileEvent Event(long id) => new(id, $"A{id}", "Person", "Gate", "Card", "Granted", "North", DateTimeOffset.UtcNow, "/photo", null);
}

public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan value) => _now += value;
}
