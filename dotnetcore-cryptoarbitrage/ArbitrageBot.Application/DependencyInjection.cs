using ArbitrageBot.Application.Hubs;
using ArbitrageBot.Application.Services;
using ArbitrageBot.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ArbitrageBot.Application;

/// <summary>
/// Métodos de extensión para registrar la capa de Application en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ─── Servicios de negocio ──────────────────────────────
        services.AddSingleton<CircuitBreaker>();
        services.AddSingleton<ProfitCalculator>();
        services.AddSingleton<IWalletManager, WalletManager>();

        // ─── BackgroundServices del pipeline ──────────────────
        // Registramos los tipos concretos como singletons para que
        // puedan ser resueltos tanto como IHostedService como por sus interfaces.
        services.AddSingleton<OrderBookAggregatorService>();
        services.AddSingleton<ArbitrageDetectorService>();
        services.AddSingleton<TradeExecutorService>();

        // Luego los registramos como hosted services
        services.AddHostedService<OrderBookAggregatorService>(
            sp => sp.GetRequiredService<OrderBookAggregatorService>());
        services.AddHostedService<ArbitrageDetectorService>(
            sp => sp.GetRequiredService<ArbitrageDetectorService>());
        services.AddHostedService<TradeExecutorService>(
            sp => sp.GetRequiredService<TradeExecutorService>());

        // Registro de interfaces que apuntan a los mismos singletons
        services.AddSingleton<IArbitrageDetector>(
            sp => sp.GetRequiredService<ArbitrageDetectorService>());
        services.AddSingleton<ITradeExecutor>(
            sp => sp.GetRequiredService<TradeExecutorService>());

        // ─── SignalR Hub ───────────────────────────────────────
        services.AddSignalR();

        return services;
    }
}
