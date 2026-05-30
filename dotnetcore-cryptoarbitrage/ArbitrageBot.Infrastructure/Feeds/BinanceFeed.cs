using System.Net.WebSockets;
using System.Text;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Feed WebSocket de Binance.
/// wss://stream.binance.com:9443/ws/btcusdt@depth5@100ms
/// </summary>
public class BinanceFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<BinanceFeed> _logger;
    private readonly string _wsUrl;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public string ExchangeId => "Binance";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public BinanceFeed(ILogger<BinanceFeed> logger)
    {
        _logger = logger;
        _wsUrl = "wss://stream.binance.com:9443/ws/btcusdt@depth5@100ms";
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await ConnectWithRetryAsync(_cts.Token);
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        var retryDelay = TimeSpan.FromSeconds(2);
        const int maxDelaySec = 30;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _ws?.Dispose();
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(_wsUrl), ct);
                _logger.LogInformation("BinanceFeed conectado a {Url}", _wsUrl);
                retryDelay = TimeSpan.FromSeconds(2); // reset en conexión exitosa
                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BinanceFeed error. Reintentando en {Delay}s...", retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, ct);
                retryDelay = TimeSpan.FromSeconds(
                    Math.Min(retryDelay.TotalSeconds * 2, maxDelaySec));
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            sb.Clear();
            WebSocketReceiveResult result;

            do
            {
                result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("BinanceFeed cerrado por el servidor");
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                break;
            }

            try
            {
                var orderBook = ParseOrderBook(sb.ToString());
                if (orderBook is not null)
                    OnOrderBookUpdated?.Invoke(orderBook);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BinanceFeed error parseando mensaje");
            }
        }
    }

    private OrderBook? ParseOrderBook(string raw)
    {
        var json = JObject.Parse(raw);
        if (json["lastUpdateId"] is null) return null;

        var bids = json["bids"]?.First?.First;
        var asks = json["asks"]?.First?.First;

        if (bids is null || asks is null) return null;

        return new OrderBook(
            ExchangeId,
            BestBid: (decimal)bids[0],
            BestAsk: (decimal)asks[0],
            BidVolume: (decimal)(json["bids"]?.First?[1] ?? 0),
            AskVolume: (decimal)(json["asks"]?.First?[1] ?? 0),
            Timestamp: DateTime.UtcNow
        );
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ws?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
