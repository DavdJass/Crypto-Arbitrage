using System.Net.WebSockets;
using System.Text;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using ArbitrageBot.Domain.Configuration;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Feed WebSocket + REST fallback de Kraken.
/// Configurable vía IOptions&lt;ExchangeOptions&gt;.
/// </summary>
public class KrakenFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<KrakenFeed> _logger;
    private readonly HttpClient _httpClient;
    private readonly FeedHealthTracker _health;
    private readonly string _wsUrl;
    private readonly string _restUrl;
    private readonly string _symbol;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _useRestFallback;
    private decimal _lastBestBid, _lastBestAsk, _lastBidVolume, _lastAskVolume;

    public string ExchangeId => "Kraken";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public KrakenFeed(
        ILogger<KrakenFeed> logger,
        HttpClient httpClient,
        FeedHealthTracker health,
        IOptions<ExchangeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _health = health;
        var cfg = options.Value.Exchanges["Kraken"];
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
                await _ws.ConnectAsync(new Uri(_wsUrl), ct);

                var subscribeMsg = $"{{\"event\":\"subscribe\",\"pair\":[\"{_symbol}\"],\"subscription\":{{\"name\":\"book\",\"depth\":10}}}}";
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

            if (result.MessageType == WebSocketMessageType.Close) break;

            try
            {
                var raw = sb.ToString();
                if (raw.StartsWith("["))
                {
                    var orderBook = ParseOrderBook(raw);
                    if (orderBook is not null)
                    {
                        _health.SetStatus(ExchangeId, "connected", "WebSocket — datos en vivo");
                        OnOrderBookUpdated?.Invoke(orderBook);
                    }
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
        var arr = JArray.Parse(raw);
        if (arr.Count < 4) return null;

        var channel = arr[2]?.ToString();
        if (channel != "book-10") return null;

        var data = arr[1] as JObject;
        if (data is null) return null;

        var asks = data["as"] ?? data["a"];
        var bids = data["bs"] ?? data["b"];

        if (asks is JArray askArray && askArray.Count > 0 && bids is JArray bidArray && bidArray.Count > 0)
        {
            _lastBestAsk = (decimal)askArray[0][0];
            _lastBestBid = (decimal)bidArray[0][0];
            _lastAskVolume = (decimal)askArray[0][1];
            _lastBidVolume = (decimal)bidArray[0][1];

            return new OrderBook(ExchangeId, _lastBestBid, _lastBestAsk, _lastBidVolume, _lastAskVolume, DateTime.UtcNow);
        }

        if (_lastBestAsk > 0 && _lastBestBid > 0)
            return new OrderBook(ExchangeId, _lastBestBid, _lastBestAsk, _lastBidVolume, _lastAskVolume, DateTime.UtcNow);

        return null;
    }

    private OrderBook? ParseRestOrderBook(string raw)
    {
        var json = JObject.Parse(raw);
        var result = json["result"];
        if (result is null) return null;

        var xbtUsd = result["XBTUSD"];
        if (xbtUsd is null) return null;

        var asks = xbtUsd["asks"] as JArray;
        var bids = xbtUsd["bids"] as JArray;

        if (asks is null || bids is null || asks.Count == 0 || bids.Count == 0)
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
