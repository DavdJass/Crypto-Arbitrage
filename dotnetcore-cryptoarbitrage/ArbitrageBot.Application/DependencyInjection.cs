using ArbitrageBot.Application.Hubs;
using ArbitrageBot.Application.Services;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Infrastructure.Cache;
using ArbitrageBot.Infrastructure.Feeds;
using ArbitrageBot.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArbitrageBot.Application;

/// <summary>
/// Métodos de extensión para registrar la capa de Application en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Servicios de negocio
        services.AddSingleton<CircuitBreaker>();
        services.AddSingleton<ProfitCalculator>();
        services.AddSingleton<IWalletManager, WalletManager>();

        // BackgroundServices del pipeline
        services.AddHostedService<OrderBookAggregatorService>();
        services.AddHostedService<ArbitrageDetectorService>();
        services.AddHostedService<TradeExecutorService>();

        // Registro de IArbitrageDetector e ITradeExecutor como singletons
        // (los mismos que los BackgroundServices)
        services.AddSingleton<IArbitrageDetector>(
            sp => sp.GetRequiredService<ArbitrageDetectorService>());
        services.AddSingleton<ITradeExecutor>(
            sp => sp.GetRequiredService<TradeExecutorService>());

        // SignalR Hub
        services.AddSignalR();

        return services;
    }
}
