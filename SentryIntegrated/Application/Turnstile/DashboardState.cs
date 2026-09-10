using Microsoft.Extensions.Options;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;

namespace SentryIntegrated.Application.Turnstile;

public sealed record DashboardSnapshot(TurnstileEvent? Spotlight, IReadOnlyList<TurnstileEvent> Queue, IReadOnlyList<TurnstileEvent> Feed, bool DatabaseAvailable);

public sealed class DashboardState(IOptions<SentryOptions> options, TimeProvider clock)
{
    private readonly object _gate = new();
    private readonly Queue<(TurnstileEvent Event, DateTimeOffset Expires)> _queue = new();
    private readonly LinkedList<TurnstileEvent> _feed = new();
    private readonly HashSet<long> _seen = [];
    private TurnstileEvent? _spotlight;
    private DateTimeOffset _spotlightExpires;
    private bool _databaseAvailable = true;
    public event Action? Changed;

    public bool Add(TurnstileEvent value)
    {
        lock (_gate)
        {
            if (!_seen.Add(value.Id)) return false;
            Advance(clock.GetUtcNow());
            if (_spotlight is null) SetSpotlight(value);
            else
            {
                if (_queue.Count >= options.Value.Dashboard.QueueLimit) _queue.Dequeue();
                _queue.Enqueue((value, DateTimeOffset.MaxValue));
            }
        }
        Changed?.Invoke();
        return true;
    }
    public void Tick()
    {
        bool changed;
        lock (_gate) changed = Advance(clock.GetUtcNow());
        if (changed) Changed?.Invoke();
    }
    public void SetDatabaseAvailable(bool available)
    {
        lock (_gate) { if (_databaseAvailable == available) return; _databaseAvailable = available; }
        Changed?.Invoke();
    }
    public DashboardSnapshot Snapshot()
    {
        lock (_gate) return new(_spotlight, _queue.Select(x => x.Event).ToArray(), _feed.ToArray(), _databaseAvailable);
    }
    private bool Advance(DateTimeOffset now)
    {
        if (_spotlight is null || now < _spotlightExpires) return false;
        _feed.AddFirst(_spotlight);
        while (_feed.Count > options.Value.Dashboard.FeedLimit) _feed.RemoveLast();
        _spotlight = null;
        if (_queue.TryDequeue(out var next)) SetSpotlight(next.Event);
        return true;
    }
    private void SetSpotlight(TurnstileEvent value)
    {
        _spotlight = value;
        _spotlightExpires = clock.GetUtcNow().AddMilliseconds(options.Value.Dashboard.SpotlightMilliseconds);
    }
}
