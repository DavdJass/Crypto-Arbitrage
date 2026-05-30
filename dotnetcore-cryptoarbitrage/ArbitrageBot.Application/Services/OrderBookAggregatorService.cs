using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// BackgroundService que recibe eventos de todos los feeds WebSocket,
/// los unifica en el caché de order books y los pushea via SignalR al frontend.
/// Es el punto de entrada del pipeline.
/// </summary>
public class OrderBookAggregatorService : BackgroundService
{
    private readonly IOrderBookAggregator _cache;
    private readonly IEnumerable<IExchangeFeed> _feeds;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<OrderBookAggregatorService> _logger;

    public OrderBookAggregatorService(
        IOrderBookAggregator cache,
        IEnumerable<IExchangeFeed> feeds,
        IHubContext<ArbitrageHub> hubContext,
        ILogger<OrderBookAggregatorService> logger)
    {
        _cache = cache;
        _feeds = feeds;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderBookAggregatorService iniciando. Feeds: {Feeds}",
            string.Join(", ", _feeds.Select(f => f.ExchangeId)));

        foreach (var feed in _feeds)
        {
            feed.OnOrderBookUpdated += orderBook =>
            {
                _cache.UpdateOrderBook(orderBook);

                // Push a todos los clientes SignalR conectados
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("OrderBookUpdated", orderBook, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error enviando OrderBookUpdated via SignalR");
                    }
                }, stoppingToken);

                _logger.LogTrace("[{Exchange}] OB actualizado | Bid={Bid:F2} Ask={Ask:F2}",
                    orderBook.ExchangeId, orderBook.BestBid, orderBook.BestAsk);
            };
        }

        var connectTasks = _feeds.Select(f => f.ConnectAsync(stoppingToken)).ToList();
        await Task.WhenAll(connectTasks);

        _logger.LogInformation("OrderBookAggregatorService finalizado");
    }
}
