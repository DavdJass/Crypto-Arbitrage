using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Infrastructure.Persistence;

/// <summary>
/// Repositorio de trades en PostgreSQL usando Dapper.
/// </summary>
public class TradeRepository : ITradeRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;
    private readonly ILogger<TradeRepository> _logger;

    public TradeRepository(PostgresConnectionFactory connectionFactory, ILogger<TradeRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task SaveAsync(TradeResult trade, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO trades (id, buy_exchange, sell_exchange, volume, net_profit, return_pct, is_profit, status, executed_at)
            VALUES (@Id, @BuyExchange, @SellExchange, @Volume, @NetProfit, @ReturnPct, @IsProfit, @Status, @ExecutedAt)
            ON CONFLICT (id) DO NOTHING;";

        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, trade, cancellationToken: ct));
        _logger.LogDebug("Trade {TradeId} guardado en Postgres", trade.Id);
    }

    public async Task<IReadOnlyList<TradeResult>> GetRecentAsync(int limit, CancellationToken ct)
    {
        const string sql = @"
            SELECT id, buy_exchange, sell_exchange, volume, net_profit, return_pct, is_profit, status, executed_at
            FROM trades
            ORDER BY executed_at DESC
            LIMIT @Limit;";

        using var conn = _connectionFactory.CreateConnection();
        var results = await conn.QueryAsync<TradeResult>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: ct));
        return results.AsList();
    }

    public async Task<(decimal totalPnl, int totalTrades, int winningTrades)> GetSummaryAsync(CancellationToken ct)
    {
        const string sql = @"
            SELECT
                COALESCE(SUM(net_profit), 0) AS TotalPnl,
                COUNT(*) AS TotalTrades,
                COUNT(*) FILTER (WHERE is_profit = true) AS WinningTrades
            FROM trades;";

        using var conn = _connectionFactory.CreateConnection();
        var result = await conn.QuerySingleAsync(new CommandDefinition(sql, cancellationToken: ct));

        return (
            (decimal)result.totalPnl,
            (int)result.totalTrades,
            (int)result.winningTrades
        );
    }
}
