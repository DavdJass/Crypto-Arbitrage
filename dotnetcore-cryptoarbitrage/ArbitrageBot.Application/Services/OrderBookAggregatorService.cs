using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ArbitrageBot.Infrastructure.Feeds;

namespace ArbitrageBot.Application.Services;

public class OrderBookAggregatorService : BackgroundService
{
    private readonly IOrderBookAggregator _cache;
    private readonly IEnumerable<IExchangeFeed> _feeds;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly FeedHealthTracker _health;
    private readonly ILogger<OrderBookAggregatorService> _logger;

    public OrderBookAggregatorService(
        IOrderBookAggregator cache,
        IEnumerable<IExchangeFeed> feeds,
        IHubContext<ArbitrageHub> hubContext,
        FeedHealthTracker health,
        ILogger<OrderBookAggregatorService> logger)
    {
        _cache = cache; _feeds = feeds; _hubContext = hubContext;
        _health = health; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("OB Aggregator iniciado. Feeds: {Feeds}",
            string.Join(", ", _feeds.Select(f => f.ExchangeId)));

        foreach (var feed in _feeds)
        {
            feed.OnOrderBookUpdated += orderBook =>
            {
                _cache.UpdateOrderBook(orderBook);

                // Latencia desde que el exchange generó el dato hasta ahora
                var latMs = (long)(DateTime.UtcNow - orderBook.Timestamp).TotalMilliseconds;
                _health.RecordLatency(orderBook.ExchangeId, latMs);

                _ = Task.Run(async () =>
                {
                    try { await _hubContext.Clients.All.SendAsync("OrderBookUpdated", orderBook, ct); }
                    catch { }
                }, ct);
            };
        }

        var connectTasks = _feeds.Select(f => f.ConnectAsync(ct)).ToList();
        await Task.WhenAll(connectTasks);
        _logger.LogInformation("OB Aggregator finalizado");
    }
}
