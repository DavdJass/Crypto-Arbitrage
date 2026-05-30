using System.Net.WebSockets;
using System.Text;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Feed WebSocket de Kraken.
/// wss://ws.kraken.com
/// Suscripción: {"event":"subscribe","pair":["XBT/USD"],"subscription":{"name":"book","depth":10}}
/// </summary>
public class KrakenFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<KrakenFeed> _logger;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public string ExchangeId => "Kraken";
    public event Action<OrderBook>? OnOrderBookUpdated;

    // Último snapshot para manejar deltas
    private decimal _lastBestBid;
    private decimal _lastBestAsk;
    private decimal _lastBidVolume;
    private decimal _lastAskVolume;

    public KrakenFeed(ILogger<KrakenFeed> logger)
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
                await _ws.ConnectAsync(new Uri("wss://ws.kraken.com"), ct);

                // Enviar suscripción
                var subscribeMsg = "{\"event\":\"subscribe\",\"pair\":[\"XBT/USD\"],\"subscription\":{\"name\":\"book\",\"depth\":10}}";
                var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

                _logger.LogInformation("KrakenFeed conectado y suscrito a XBT/USD");
                retryDelay = TimeSpan.FromSeconds(2);
                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KrakenFeed error. Reintentando en {Delay}s...", retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, ct);
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, maxDelaySec));
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
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
                _logger.LogWarning("KrakenFeed cerrado por el servidor");
                break;
            }

            try
            {
                var raw = sb.ToString();
                if (raw.StartsWith("["))
                {
                    var orderBook = ParseOrderBook(raw);
                    if (orderBook is not null)
                        OnOrderBookUpdated?.Invoke(orderBook);
                }
                // Ignorar mensajes de evento (subscriptionStatus, heartbeat, etc.)
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KrakenFeed error parseando mensaje");
            }
        }
    }

    private OrderBook? ParseOrderBook(string raw)
    {
        var arr = JArray.Parse(raw);
        if (arr.Count < 4) return null;

        var channel = arr[2]?.ToString();
        if (channel != "book-10") return null;

        var data = arr[1] as JObject;
        if (data is null) return null;

        // Kraken envía: as = asks, bs = bids (snapshot) o a = ask delta, b = bid delta
        // Intentar snapshot primero
        var asks = data["as"] ?? data["a"];
        var bids = data["bs"] ?? data["b"];

        if (asks is JArray askArray && askArray.Count > 0 && bids is JArray bidArray && bidArray.Count > 0)
        {
            var bestAsk = (decimal)askArray[0][0];
            var bestBid = (decimal)bidArray[0][0];
            var askVol = (decimal)askArray[0][1];
            var bidVol = (decimal)bidArray[0][1];

            _lastBestAsk = bestAsk;
            _lastBestBid = bestBid;
            _lastAskVolume = askVol;
            _lastBidVolume = bidVol;

            return new OrderBook(ExchangeId, bestBid, bestAsk, bidVol, askVol, DateTime.UtcNow);
        }

        // Si es delta, usar los últimos valores conocidos
        if (_lastBestAsk > 0 && _lastBestBid > 0)
        {
            return new OrderBook(ExchangeId, _lastBestBid, _lastBestAsk, _lastBidVolume, _lastAskVolume, DateTime.UtcNow);
        }

        return null;
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
