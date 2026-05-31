using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

public interface IOpportunityRepository
{
    Task SaveAsync(StoredOpportunity opportunity, CancellationToken ct = default);
    Task<IReadOnlyList<StoredOpportunity>> GetRecentAsync(int limit, CancellationToken ct = default);
}
