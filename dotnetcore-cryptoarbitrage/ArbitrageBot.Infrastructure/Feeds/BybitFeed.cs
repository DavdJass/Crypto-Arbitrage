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
/// Feed WebSocket + REST fallback de Bybit (v5 spot).
/// Configurable vía IOptions&lt;ExchangeOptions&gt;.
/// </summary>
public class BybitFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<BybitFeed> _logger;
    private readonly HttpClient _httpClient;
    private readonly FeedHealthTracker _health;
    private readonly string _wsUrl;
    private readonly string _restUrl;
    private readonly string _symbol;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _useRestFallback;

    public string ExchangeId => "Bybit";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public BybitFeed(
        ILogger<BybitFeed> logger,
        HttpClient httpClient,
        FeedHealthTracker health,
        IOptions<ExchangeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _health = health;
        var cfg = options.Value.Exchanges["Bybit"];
        _wsUrl = cfg.WebSocketUrl;
        _restUrl = cfg.RestUrl;
        _symbol = cfg.Symbol;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _health.SetStatus(ExchangeId, "connecting");
        _ = Task.Run(() => ConnectWithRetryAsync(_cts.Token), ct);
        await Task.CompletedTask;
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
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

                var subscribeMsg = $"{{\"op\":\"subscribe\",\"args\":[\"orderbook.1.{_symbol}\"]}}";
                var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

                _health.SetStatus(ExchangeId, "connected", "WebSocket conectado");
                _useRestFallback = false;
                _logger.LogInformation("[{Exchange}] WebSocket conectado a {Url}", ExchangeId, _wsUrl);
                retryDelay = TimeSpan.FromSeconds(2);

                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Exchange}] WebSocket error. Cambiando a REST fallback...", ExchangeId);
                _useRestFallback = true;
                await StartRestFallbackAsync(ct);

                _health.SetStatus(ExchangeId, "connecting", $"Reintentando WS en {retryDelay.TotalSeconds}s...");
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

            if (result.MessageType == WebSocketMessageType.Close) break;

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

    private async Task StartRestFallbackAsync(CancellationToken ct)
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

    private OrderBook? ParseRestOrderBook(string raw)
    {
        var json = JObject.Parse(raw);
        var result = json["result"];
        if (result is null) return null;

        // Bybit v5 REST response tiene "result": { "b": [...], "a": [...] }
        var bids = result["b"] as JArray;
        var asks = result["a"] as JArray;

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
        _useRestFallback = false;
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
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
