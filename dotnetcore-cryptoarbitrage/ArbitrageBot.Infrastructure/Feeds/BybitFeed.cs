using System.Net.WebSockets;
using System.Text;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Feed WebSocket de Bybit.
/// wss://stream.bybit.com/v5/public/spot
/// Suscripción: {"op":"subscribe","args":["orderbook.1.BTCUSDT"]}
/// </summary>
public class BybitFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<BybitFeed> _logger;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public string ExchangeId => "Bybit";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public BybitFeed(ILogger<BybitFeed> logger)
    {
        _logger = logger;
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
                await _ws.ConnectAsync(new Uri("wss://stream.bybit.com/v5/public/spot"), ct);

                var subscribeMsg = "{\"op\":\"subscribe\",\"args\":[\"orderbook.1.BTCUSDT\"]}";
                var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

                _logger.LogInformation("BybitFeed conectado y suscrito a BTCUSDT");
                retryDelay = TimeSpan.FromSeconds(2);
                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BybitFeed error. Reintentando en {Delay}s...", retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, ct);
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, maxDelaySec));
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
                _logger.LogWarning("BybitFeed cerrado por el servidor");
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
                _logger.LogWarning(ex, "BybitFeed error parseando mensaje");
            }
        }
    }

    private OrderBook? ParseOrderBook(string raw)
    {
        var json = JObject.Parse(raw);
        if (json["type"]?.ToString() != "snapshot" && json["type"]?.ToString() != "delta")
            return null;

        var data = json["data"];
        if (data is null) return null;

        var bids = data["b"] as JArray;
        var asks = data["a"] as JArray;

        if (bids is null || asks is null || bids.Count == 0 || asks.Count == 0)
            return null;

        return new OrderBook(
            ExchangeId,
            BestBid: (decimal)bids[0][0],
            BestAsk: (decimal)asks[0][0],
            BidVolume: (decimal)bids[0][1],
            AskVolume: (decimal)asks[0][1],
            Timestamp: DateTime.UtcNow
        );
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
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
