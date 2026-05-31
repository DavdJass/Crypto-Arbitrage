using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Infrastructure.Cache;
using ArbitrageBot.Infrastructure.Feeds;
using ArbitrageBot.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, string postgresConnectionString, string? sqliteDbPath = null)
    {
        services.AddSingleton<FeedHealthTracker>();

        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5),
            DefaultRequestHeaders = { { "User-Agent", "ArbitrageBot/1.0" } }
        });

        // 10 feeds
        services.AddSingleton<IExchangeFeed, BinanceFeed>();
        services.AddSingleton<IExchangeFeed, KrakenFeed>();
        services.AddSingleton<IExchangeFeed, BybitFeed>();
        services.AddSingleton<IExchangeFeed, CoinbaseFeed>();
        services.AddSingleton<IExchangeFeed, OkxFeed>();
        services.AddSingleton<IExchangeFeed, BitfinexFeed>();
        services.AddSingleton<IExchangeFeed, KuCoinFeed>();
        services.AddSingleton<IExchangeFeed, GateIoFeed>();
        services.AddSingleton<IExchangeFeed, BitstampFeed>();
        services.AddSingleton<IExchangeFeed, GeminiFeed>();

        services.AddSingleton<IOrderBookAggregator, MemoryOrderBookCache>();

        // Persistencia: en memoria (rapido, sin dependencias externas)
        services.AddSingleton<ITradeRepository, InMemoryTradeRepository>();

        // Para usar SQLite en produccion, descomentar:
        // var dbPath = sqliteDbPath ?? Path.Combine(Directory.GetCurrentDirectory(), "arbitrage.db");
        // services.AddSingleton(sp =>
        //     new SqliteTradeRepository(dbPath, sp.GetRequiredService<ILogger<SqliteTradeRepository>>()));
        // services.AddSingleton<ITradeRepository>(sp => sp.GetRequiredService<SqliteTradeRepository>());

        return services;
    }
}
