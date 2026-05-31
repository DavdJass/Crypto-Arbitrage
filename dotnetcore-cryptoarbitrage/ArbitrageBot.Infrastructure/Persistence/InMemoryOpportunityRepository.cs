using System.Collections.Concurrent;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Infrastructure.Persistence;

public class InMemoryOpportunityRepository : IOpportunityRepository
{
    private readonly ConcurrentBag<StoredOpportunity> _opportunities = new();
    private const int MaxItems = 10_000;

    public Task SaveAsync(StoredOpportunity opportunity, CancellationToken ct = default)
    {
        _opportunities.Add(opportunity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredOpportunity>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var result = _opportunities
            .OrderByDescending(o => o.DetectedAt)
            .Take(Math.Max(1, limit))
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<StoredOpportunity>>(result);
    }
}
