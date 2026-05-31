using System.Net.WebSockets;
using System.Text;
using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Feed WebSocket + REST fallback de Bitfinex.
/// WebSocket: book channel P0, top-of-book.
/// REST: v2 ticker como fallback cada 2s.
/// </summary>
public sealed class BitfinexFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<BitfinexFeed> _logger;
    private readonly HttpClient _httpClient;
    private readonly FeedHealthTracker _health;
    private readonly string _wsUrl;
    private readonly string _restUrl;
    private readonly string _symbol;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _useRestFallback;

    public string ExchangeId => "Bitfinex";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public BitfinexFeed(
        ILogger<BitfinexFeed> logger,
        HttpClient httpClient,
        FeedHealthTracker health,
        IOptions<ExchangeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _health = health;
        var cfg = options.Value.Exchanges["Bitfinex"];
        _wsUrl = cfg.WebSocketUrl;
        _restUrl = cfg.RestUrl;
        _symbol = cfg.Symbol;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _health.SetStatus(ExchangeId, "connecting");
        _ = Task.Run(() => ConnectLoopAsync(_cts.Token), ct);
        await Task.CompletedTask;
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        var retryDelay = TimeSpan.FromSeconds(2);
        const int maxDelaySec = 30;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _health.SetStatus(ExchangeId, "connecting", "Intentando WebSocket...");

                _ws?.Dispose();
                _ws = new ClientWebSocket();
                _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                await _ws.ConnectAsync(new Uri(_wsUrl), ct);

                var subscribeMsg = Encoding.UTF8.GetBytes(
                    $"{{\"event\":\"subscribe\",\"channel\":\"book\",\"symbol\":\"{_symbol}\"," +
                    "\"prec\":\"P0\",\"freq\":\"F0\",\"len\":\"1\"}");
                await _ws.SendAsync(new ArraySegment<byte>(subscribeMsg),
                    WebSocketMessageType.Text, true, ct);

                _health.SetStatus(ExchangeId, "connected", "WebSocket conectado");
                _useRestFallback = false;
                _logger.LogInformation("[{Exchange}] WebSocket conectado", ExchangeId);
                retryDelay = TimeSpan.FromSeconds(2);

                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Exchange}] WS error → REST fallback", ExchangeId);
                _useRestFallback = true;
                await RestFallbackLoopAsync(ct);
                _health.SetStatus(ExchangeId, "connecting",
                    $"Reintentando WS en {retryDelay.TotalSeconds}s...");
                await Task.Delay(retryDelay, ct);
                retryDelay = TimeSpan.FromSeconds(
                    Math.Min(retryDelay.TotalSeconds * 2, maxDelaySec));
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[32768];
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
                _logger.LogWarning("[{Exchange}] WebSocket cerrado por el servidor", ExchangeId);
                break;
            }

            try
            {
                var orderBook = ParseOrderBook(sb.ToString());
                if (orderBook is not null)
                {
                    _health.SetStatus(ExchangeId, "connected", "WebSocket — datos en vivo");
                    OnOrderBookUpdated?.Invoke(orderBook);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Exchange}] Error parseando mensaje WS", ExchangeId);
            }
        }
    }

    private async Task RestFallbackLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("[{Exchange}] Iniciando REST fallback cada 2s...", ExchangeId);
        _health.SetStatus(ExchangeId, "fallback_rest", "Polling REST API cada 2s");

        while (_useRestFallback && !ct.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(_restUrl, ct);
                var orderBook = ParseRestOrderBook(response);
                if (orderBook is not null)
                {
                    OnOrderBookUpdated?.Invoke(orderBook);
                    _health.SetStatus(ExchangeId, "fallback_rest", "REST — datos recibidos");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Exchange}] REST fallback error", ExchangeId);
                _health.SetStatus(ExchangeId, "fallback_rest", $"Error: {ex.Message}");
            }
            await Task.Delay(2000, ct);
        }
    }

    private OrderBook? ParseOrderBook(string raw)
    {
        try
        {
            var token = JToken.Parse(raw);
            if (token is JArray arr && arr.Count > 1 && arr[1] is JArray data)
            {
                // Ignorar heartbeats
                if (arr[0]?.ToString() == "hb") return null;

                // data[0] = bids (top entry), data[2] = asks (top entry)
                if (data.Count >= 3 && data[0] is JArray bid && data[2] is JArray ask)
                {
                    return new OrderBook(
                        ExchangeId,
                        BestBid: (decimal)bid[0],
                        BestAsk: (decimal)ask[0],
                        BidVolume: (decimal)bid[1],
                        AskVolume: (decimal)ask[1],
                        Timestamp: DateTime.UtcNow);
                }
            }
        }
        catch { }
        return null;
    }

    private OrderBook? ParseRestOrderBook(string raw)
    {
        try
        {
            // v2 ticker: [BID, BID_SIZE, ASK, ASK_SIZE, ...]
            var arr = JArray.Parse(raw);
            if (arr.Count < 4) return null;
            return new OrderBook(
                ExchangeId,
                BestBid: (decimal)arr[0],
                BestAsk: (decimal)arr[2],
                BidVolume: (decimal)(arr[1] ?? 0),
                AskVolume: (decimal)(arr[3] ?? 0),
                Timestamp: DateTime.UtcNow);
        }
        catch { return null; }
    }

    public async Task DisconnectAsync()
    {
        _useRestFallback = false;
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "",
                CancellationToken.None);
        _health.SetStatus(ExchangeId, "disconnected", "Desconexión manual");
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
