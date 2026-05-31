using Dapper;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Infrastructure.Persistence;

public class SqliteOpportunityRepository : IOpportunityRepository
{
    private readonly string _connStr;
    private readonly ILogger<SqliteOpportunityRepository> _logger;

    public SqliteOpportunityRepository(string dbPath, ILogger<SqliteOpportunityRepository> logger)
    {
        _connStr = $"Data Source={dbPath}";
        _logger = logger;
        Init();
    }

    private void Init()
    {
        using var c = new SqliteConnection(_connStr);
        c.Open();
        c.Execute(@"
            CREATE TABLE IF NOT EXISTS opportunities(
                id TEXT PRIMARY KEY,
                buy_exchange TEXT NOT NULL,
                sell_exchange TEXT NOT NULL,
                ask_price REAL NOT NULL,
                bid_price REAL NOT NULL,
                volume REAL NOT NULL,
                net_profit REAL NOT NULL,
                return_pct REAL NOT NULL,
                status TEXT NOT NULL,
                reason TEXT,
                detected_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_opportunities_at ON opportunities(detected_at DESC);");
        _logger.LogInformation("SQLite opportunities table ready: {Db}", _connStr);
    }

    public async Task SaveAsync(StoredOpportunity opportunity, CancellationToken ct = default)
    {
        using var c = new SqliteConnection(_connStr);
        await c.ExecuteAsync(@"
            INSERT OR IGNORE INTO opportunities(
                id, buy_exchange, sell_exchange, ask_price, bid_price, volume,
                net_profit, return_pct, status, reason, detected_at)
            VALUES(
                @Id, @BuyExchange, @SellExchange, @AskPrice, @BidPrice, @Volume,
                @NetProfit, @ReturnPct, @Status, @Reason, @DetectedAt)",
            opportunity);
    }

    public async Task<IReadOnlyList<StoredOpportunity>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        using var c = new SqliteConnection(_connStr);
        var rows = await c.QueryAsync<StoredOpportunity>(@"
            SELECT id AS Id, buy_exchange AS BuyExchange, sell_exchange AS SellExchange,
                   ask_price AS AskPrice, bid_price AS BidPrice, volume AS Volume,
                   net_profit AS NetProfit, return_pct AS ReturnPct, status AS Status,
                   reason AS Reason, detected_at AS DetectedAt
            FROM opportunities ORDER BY detected_at DESC LIMIT @Limit",
            new { Limit = limit });
        return rows.AsList();
    }
}
