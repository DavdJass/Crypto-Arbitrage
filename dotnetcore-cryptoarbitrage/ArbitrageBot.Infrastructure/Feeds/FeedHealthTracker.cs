using System.Collections.Concurrent;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>Monitorea el estado y latencia de cada feed.</summary>
public class FeedHealthTracker
{
    private readonly ConcurrentDictionary<string, FeedStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, LinkedList<long>> _latencies = new();

    public void SetStatus(string exchangeId, string status, string details = "")
    {
        _statuses[exchangeId] = new FeedStatus(exchangeId, status, details, DateTime.UtcNow);
    }

    public void RecordLatency(string exchangeId, long ms)
    {
        var list = _latencies.GetOrAdd(exchangeId, _ => new LinkedList<long>());
        lock (list)
        {
            list.AddLast(ms);
            while (list.Count > 100) list.RemoveFirst();
        }
    }

    /// <summary>Latencia promedio de los últimos eventos (ms).</summary>
    public double GetAvgLatency(string exchangeId)
    {
        if (!_latencies.TryGetValue(exchangeId, out var list)) return 0;
        lock (list)
        {
            return list.Count > 0 ? list.Average() : 0;
        }
    }

    public FeedStatus GetStatus(string exchangeId)
    {
        var s = _statuses.GetValueOrDefault(exchangeId, new FeedStatus(exchangeId, "unknown", "", DateTime.UtcNow));
        return s with { AvgLatencyMs = GetAvgLatency(exchangeId) };
    }

    public IReadOnlyDictionary<string, FeedStatus> GetAllStatuses()
    {
        var result = new Dictionary<string, FeedStatus>();
        foreach (var key in _statuses.Keys)
            result[key] = GetStatus(key);
        return result;
    }
}

public record FeedStatus(string ExchangeId, string Status, string Details, DateTime LastUpdated)
{
    public double AvgLatencyMs { get; init; }
}
