using Npgsql;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.API;

/// <summary>
/// Aplica migraciones automáticas al arrancar (crea tabla trades si no existe).
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(string connectionString, ILogger logger)
    {
        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                CREATE TABLE IF NOT EXISTS trades (
                    id              UUID PRIMARY KEY,
                    buy_exchange    VARCHAR(20) NOT NULL,
                    sell_exchange   VARCHAR(20) NOT NULL,
                    volume          DECIMAL(18,8) NOT NULL,
                    net_profit      DECIMAL(18,8) NOT NULL,
                    return_pct      DECIMAL(18,8) NOT NULL,
                    is_profit       BOOLEAN NOT NULL,
                    status          VARCHAR(30) NOT NULL,
                    executed_at     TIMESTAMPTZ NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_trades_executed_at ON trades (executed_at DESC);
            ";
            await conn.ExecuteAsync(sql);
            logger.LogInformation("Migración de base de datos completada exitosamente");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo ejecutar la migración de BD. Puede ser normal si Postgres no está disponible aún.");
        }
    }
}

/// <summary>
/// Extensión para ejecutar SQL sin Dapper (evita dependencia en API).
/// </summary>
internal static class NpgsqlExtensions
{
    public static async Task ExecuteAsync(this NpgsqlConnection conn, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
