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
/// Feed WebSocket + REST fallback de Gemini.
/// WebSocket: l2 canal (level-2 updates). Acumula best bid/ask via deltas.
/// REST: book limit=1 como fallback cada 1.5s.
/// </summary>
public sealed class GeminiFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<GeminiFeed> _logger;
    private readonly HttpClient _httpClient;
    private readonly FeedHealthTracker _health;
    private readonly string _wsUrl;
    private readonly string _restUrl;
    private readonly string _symbol;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _useRestFallback;

    // Gemini l2 envia deltas individuales — acumulamos best bid/ask
    private decimal _lastBestBid;
    private decimal _lastBestAsk;
    private decimal _lastBidVolume;
    private decimal _lastAskVolume;

    public string ExchangeId => "Gemini";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public GeminiFeed(
        ILogger<GeminiFeed> logger,
        HttpClient httpClient,
        FeedHealthTracker health,
        IOptions<ExchangeOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _health = health;
        var cfg = options.Value.Exchanges["Gemini"];
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

                var subscribeMsg = $"{{\"type\":\"subscribe\"," +
                    $"\"subscriptions\":[{{\"name\":\"l2\",\"symbols\":[\"{_symbol}\"]}}]}}";
                await _ws.SendAsync(Encoding.UTF8.GetBytes(subscribeMsg),
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

    private async Task RestFallbackLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("[{Exchange}] Iniciando REST fallback cada 1.5s...", ExchangeId);
        _health.SetStatus(ExchangeId, "fallback_rest", "Polling REST API cada 1.5s");

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
            await Task.Delay(1500, ct);
        }
    }

    /// <summary>
    /// Gemini l2 envia cambios individuales: [side, price, quantity].
    /// Acumulamos best bid/ask comparando precios.
    /// </summary>
    private OrderBook? ParseOrderBook(string raw)
    {
        try
        {
            var json = JObject.Parse(raw);
            var changes = json["changes"]?.First;
            if (changes is null) return null;

            var side = changes[0]?.ToString();
            var price = (decimal)(changes[1] ?? 0);
            var qty = (decimal)(changes[2] ?? 0);

            if (side == "bid" && price > _lastBestBid)
            {
                _lastBestBid = price;
                _lastBidVolume = qty;
            }
            else if (side == "ask" && (_lastBestAsk == 0 || price < _lastBestAsk))
            {
                _lastBestAsk = price;
                _lastAskVolume = qty;
            }

            if (_lastBestBid > 0 && _lastBestAsk > 0)
            {
                return new OrderBook(ExchangeId, _lastBestBid, _lastBestAsk,
                    _lastBidVolume, _lastAskVolume, DateTime.UtcNow);
            }
            return null;
        }
        catch { return null; }
    }

    private OrderBook? ParseRestOrderBook(string raw)
    {
        try
        {
            var json = JObject.Parse(raw);
            var bids = json["bids"]?.First;
            var asks = json["asks"]?.First;
            if (bids is null || asks is null) return null;

            return new OrderBook(
                ExchangeId,
                BestBid: (decimal)bids[0],
                BestAsk: (decimal)asks[0],
                BidVolume: (decimal)(bids[1] ?? 0),
                AskVolume: (decimal)(asks[1] ?? 0),
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
