using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Feed REST polling de KuCoin.
/// KuCoin requiere un token dinamico via REST para WebSocket.
/// Para simplificar usamos REST polling cada 1.5s (mismo rendimiento que WS
/// para deteccion de arbitraje con <2s de latencia).
/// Endpoint: /api/v1/market/orderbook/level1
/// </summary>
public sealed class KuCoinFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<KuCoinFeed> _logger;
    private readonly HttpClient _httpClient;
    private readonly FeedHealthTracker _health;
    private readonly string _symbol;
    private readonly string _restBaseUrl;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public string ExchangeId => "KuCoin";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public KuCoinFeed(
        ILogger<KuCoinFeed> logger,
        HttpClient httpClient,
        FeedHealthTracker health,
        IOptions<ExchangeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _health = health;
        var cfg = options.Value.Exchanges["KuCoin"];
        _symbol = cfg.Symbol;
        _restBaseUrl = cfg.RestUrl;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _health.SetStatus(ExchangeId, "fallback_rest", "REST polling cada 1.5s");
        _logger.LogInformation("[{Exchange}] Usando REST polling (WS requiere token dinamico)",
            ExchangeId);
        _ = Task.Run(() => RestPollingLoopAsync(_cts.Token), ct);
        await Task.CompletedTask;
    }

    private async Task RestPollingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(
                    $"{_restBaseUrl}{_symbol}", ct);
                var json = JObject.Parse(response);
                var data = json["data"];

                if (data is not null)
                {
                    var orderBook = new OrderBook(
                        ExchangeId,
                        BestBid: (decimal)(data["bestBid"] ?? 0),
                        BestAsk: (decimal)(data["bestAsk"] ?? 0),
                        BidVolume: (decimal)(data["bestBidSize"] ?? 0),
                        AskVolume: (decimal)(data["bestAskSize"] ?? 0),
                        Timestamp: DateTime.UtcNow);

                    OnOrderBookUpdated?.Invoke(orderBook);
                    _health.SetStatus(ExchangeId, "fallback_rest", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[KuCoin] REST polling error");
                _health.SetStatus(ExchangeId, "fallback_rest",
                    $"Error: {ex.Message}");
            }
            await Task.Delay(1500, ct);
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        _health.SetStatus(ExchangeId, "disconnected", "Desconexion manual");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
