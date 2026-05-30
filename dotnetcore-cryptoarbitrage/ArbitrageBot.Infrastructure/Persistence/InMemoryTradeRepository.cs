using System.Collections.Concurrent;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Infrastructure.Persistence;

/// <summary>
/// Repositorio de trades en memoria (útil para desarrollo sin PostgreSQL).
/// Thread-safe gracias a ConcurrentBag.
/// </summary>
public class InMemoryTradeRepository : ITradeRepository
{
    private readonly ConcurrentBag<TradeResult> _trades = new();

    public Task SaveAsync(TradeResult trade, CancellationToken ct)
    {
        _trades.Add(trade);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TradeResult>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var result = _trades
            .OrderByDescending(t => t.ExecutedAt)
            .Take(limit)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<TradeResult>>(result);
    }

    public Task<(decimal totalPnl, int totalTrades, int winningTrades)> GetSummaryAsync(CancellationToken ct)
    {
        var totalTrades = _trades.Count;
        var winningTrades = _trades.Count(t => t.IsProfit);
        var totalPnl = _trades.Sum(t => t.NetProfit);

        return Task.FromResult((totalPnl, totalTrades, winningTrades));
    }
}
