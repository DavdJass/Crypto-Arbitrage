using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

/// <summary>
/// Repositorio de trades ejecutados (Postgres).
/// </summary>
public interface ITradeRepository
{
    Task SaveAsync(TradeResult trade, CancellationToken ct);
    Task<IReadOnlyList<TradeResult>> GetRecentAsync(int limit, CancellationToken ct);
    Task<(decimal totalPnl, int totalTrades, int winningTrades)> GetSummaryAsync(CancellationToken ct);
}
