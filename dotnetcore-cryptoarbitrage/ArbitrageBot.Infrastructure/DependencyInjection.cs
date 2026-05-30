using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Infrastructure.Cache;
using ArbitrageBot.Infrastructure.Feeds;
using ArbitrageBot.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArbitrageBot.Infrastructure;

/// <summary>
/// Métodos de extensión para registrar la capa de Infrastructure en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, string postgresConnectionString)
    {
        // Health tracker de conexiones (compartido entre todos los feeds)
        services.AddSingleton<FeedHealthTracker>();

        // HttpClient singleton compartido para REST fallback de todos los feeds
        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5),
            DefaultRequestHeaders = { { "User-Agent", "ArbitrageBot/1.0" } }
        });

        // ─── 10 FEEDS: WebSocket + REST fallback ──────────────
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

        // Cache de order books — singleton compartido entre feeds y detector
        services.AddSingleton<IOrderBookAggregator, MemoryOrderBookCache>();

        // ─── Persistencia ─────────────────────────────────────
        // Por defecto usa InMemoryTradeRepository para que funcione sin PostgreSQL.
        // Cuando tengas Postgres, cambia a TradeRepository.
        services.AddSingleton<ITradeRepository, InMemoryTradeRepository>();

        return services;
    }
}
