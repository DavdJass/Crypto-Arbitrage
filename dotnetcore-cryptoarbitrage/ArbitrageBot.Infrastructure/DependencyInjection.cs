using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Infrastructure.Cache;
using ArbitrageBot.Infrastructure.Feeds;
using ArbitrageBot.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        // Feeds WebSocket + REST fallback — registrados como singletons
        services.AddSingleton<IExchangeFeed, BinanceFeed>();
        services.AddSingleton<IExchangeFeed, KrakenFeed>();
        services.AddSingleton<IExchangeFeed, BybitFeed>();

        // Cache de order books — singleton compartido entre feeds y detector
        services.AddSingleton<IOrderBookAggregator, MemoryOrderBookCache>();

        // ─── Persistencia ─────────────────────────────────────
        // Por defecto usa InMemoryTradeRepository para que funcione sin PostgreSQL.
        // Cuando tengas Postgres, cambia a TradeRepository y descomenta PostgresConnectionFactory.
        services.AddSingleton<ITradeRepository, InMemoryTradeRepository>();

        // Para PostgreSQL (cuando esté disponible):
        // services.AddSingleton(new PostgresConnectionFactory(postgresConnectionString));
        // services.AddSingleton<ITradeRepository, TradeRepository>();

        return services;
    }
}
