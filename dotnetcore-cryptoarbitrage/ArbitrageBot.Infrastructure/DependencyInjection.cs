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
        // Feeds WebSocket — registrados como singletons
        services.AddSingleton<IExchangeFeed, BinanceFeed>();
        services.AddSingleton<IExchangeFeed, KrakenFeed>();
        services.AddSingleton<IExchangeFeed, BybitFeed>();

        // Cache de order books — singleton compartido entre feeds y detector
        services.AddSingleton<IOrderBookAggregator, MemoryOrderBookCache>();

        // Persistencia
        services.AddSingleton(new PostgresConnectionFactory(postgresConnectionString));
        services.AddSingleton<ITradeRepository, TradeRepository>();

        return services;
    }
}
