using Dapper;using ArbitrageBot.Domain.Interfaces;using ArbitrageBot.Domain.Models;using Microsoft.Data.Sqlite;using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Infrastructure.Persistence;

public class SqliteTradeRepository : ITradeRepository
{
    private readonly string _connStr;private readonly ILogger<SqliteTradeRepository> _l;
    public SqliteTradeRepository(string dbPath, ILogger<SqliteTradeRepository> l)
    {_connStr=$"Data Source={dbPath}";_l=l;Init();}

    void Init()
    {
        using var c = new SqliteConnection(_connStr);c.Open();
        c.Execute(@"
            CREATE TABLE IF NOT EXISTS trades(
                id TEXT PRIMARY KEY,buy_exchange TEXT NOT NULL,sell_exchange TEXT NOT NULL,
                volume REAL NOT NULL,net_profit REAL NOT NULL,return_pct REAL NOT NULL,
                is_profit INTEGER NOT NULL,status TEXT NOT NULL,executed_at TEXT NOT NULL
            );CREATE INDEX IF NOT EXISTS idx_trades_at ON trades(executed_at DESC);");
        _l.LogInformation("SQLite inicializado en: {Db}",_connStr);
    }

    public async Task SaveAsync(TradeResult t,CancellationToken ct)
    {
        using var c = new SqliteConnection(_connStr);
        await c.ExecuteAsync(@"
            INSERT OR IGNORE INTO trades(id,buy_exchange,sell_exchange,volume,net_profit,return_pct,is_profit,status,executed_at)
            VALUES(@Id,@BuyExchange,@SellExchange,@Volume,@NetProfit,@ReturnPct,@IsProfit,@Status,@ExecutedAt)",t);
    }

    public async Task<IReadOnlyList<TradeResult>> GetRecentAsync(int limit,CancellationToken ct)
    {
        using var c = new SqliteConnection(_connStr);
        var r = await c.QueryAsync<TradeResult>(@"
            SELECT id AS Id, buy_exchange AS BuyExchange, sell_exchange AS SellExchange,
                   volume AS Volume, net_profit AS NetProfit, return_pct AS ReturnPct,
                   is_profit AS IsProfit, status AS Status, executed_at AS ExecutedAt
            FROM trades ORDER BY executed_at DESC LIMIT @Limit",new{Limit=limit});
        return r.AsList();
    }

    public async Task<(decimal totalPnl,int totalTrades,int winningTrades)> GetSummaryAsync(CancellationToken ct)
    {
        using var c = new SqliteConnection(_connStr);
        var r = await c.QuerySingleAsync(@"
            SELECT COALESCE(SUM(net_profit),0) TotalPnl,COUNT(*) TotalTrades,COALESCE(SUM(is_profit),0) WinningTrades FROM trades");
        return ((decimal)r.TotalPnl,(int)r.TotalTrades,(int)r.WinningTrades);
    }
}
