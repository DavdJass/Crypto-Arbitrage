using System.Net.WebSockets;
using System.Text;
using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>Feed WebSocket + REST fallback de Coinbase (Advanced Trade).</summary>
public class CoinbaseFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<CoinbaseFeed> _l;
    private readonly HttpClient _hc;
    private readonly FeedHealthTracker _h;
    private readonly string _ws, _rest, _symbol;
    private ClientWebSocket? _wsCli;
    private CancellationTokenSource? _cts;
    private bool _d, _fallback;

    public string ExchangeId => "Coinbase";
    public event Action<OrderBook>? OnOrderBookUpdated;

    public CoinbaseFeed(ILogger<CoinbaseFeed> l, HttpClient hc, FeedHealthTracker h,
        IOptions<ExchangeOptions> o)
    {
        _l = l; _hc = hc; _h = h;
        var c = o.Value.Exchanges["Coinbase"];
        _ws = c.WebSocketUrl; _rest = c.RestUrl; _symbol = c.Symbol;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _h.SetStatus(ExchangeId, "connecting");
        _ = Task.Run(() => ConnectLoop(_cts.Token), ct);
        await Task.CompletedTask;
    }

    async Task ConnectLoop(CancellationToken ct)
    {
        var d = 2;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _h.SetStatus(ExchangeId, "connecting");
                _wsCli?.Dispose();
                _wsCli = new ClientWebSocket();
                _wsCli.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                await _wsCli.ConnectAsync(new Uri(_ws), ct);
                var sub = $"{{\"type\":\"subscribe\",\"product_ids\":[\"{_symbol}\"],\"channel\":\"ticker\"}}";
                await _wsCli.SendAsync(Encoding.UTF8.GetBytes(sub), WebSocketMessageType.Text, true, ct);
                _h.SetStatus(ExchangeId, "connected", "WebSocket");
                _fallback = false;
                _l.LogInformation("[{X}] WebSocket conectado", ExchangeId);
                d = 2; await ReceiveLoop(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _l.LogError(ex, "[{X}] WS error → REST fallback", ExchangeId);
                _fallback = true;
                await RestLoop(ct);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(d, 30)), ct);
                d *= 2;
            }
        }
    }

    async Task ReceiveLoop(CancellationToken ct)
    {
        var buf = new byte[8192];
        var sb = new StringBuilder();
        while (_wsCli?.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            sb.Clear(); WebSocketReceiveResult r;
            do { r = await _wsCli.ReceiveAsync(buf, ct);
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
            } while (!r.EndOfMessage);
            if (r.MessageType == WebSocketMessageType.Close) break;
            try { var ob = Parse(sb.ToString()); if (ob != null) { _h.SetStatus(ExchangeId, "connected", "WS"); OnOrderBookUpdated?.Invoke(ob); } } catch { }
        }
    }

    async Task RestLoop(CancellationToken ct)
    {
        _l.LogInformation("[{X}] REST fallback 2s", ExchangeId);
        _h.SetStatus(ExchangeId, "fallback_rest");
        while (_fallback && !ct.IsCancellationRequested)
        {
            try { var r = await _hc.GetStringAsync(_rest, ct); var ob = ParseRest(r); if (ob != null) { OnOrderBookUpdated?.Invoke(ob); _h.SetStatus(ExchangeId, "fallback_rest", "REST"); } } catch { }
            await Task.Delay(2000, ct);
        }
    }

    OrderBook? Parse(string raw)
    {
        var j = JObject.Parse(raw);
        if (j["type"]?.ToString() != "ticker") return null;
        var bid = j["bid"];
        var ask = j["ask"];
        if (bid == null || ask == null) return null;
        return new OrderBook(ExchangeId, (decimal)bid, (decimal)ask,
            (decimal)(j["bid_size"] ?? 0), (decimal)(j["ask_size"] ?? 0), DateTime.UtcNow);
    }

    OrderBook? ParseRest(string raw)
    {
        var j = JObject.Parse(raw);
        var bids = j["bids"]?.First;
        var asks = j["asks"]?.First;
        if (bids == null || asks == null) return null;
        return new OrderBook(ExchangeId, (decimal)bids[0], (decimal)asks[0],
            (decimal)(bids[1] ?? 0), (decimal)(asks[1] ?? 0), DateTime.UtcNow);
    }

    public async Task DisconnectAsync()
    {
        _fallback = false; _cts?.Cancel();
        if (_wsCli?.State == WebSocketState.Open)
            await _wsCli.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    public void Dispose()
    {
        if (_d) return; _d = true;
        _wsCli?.Dispose(); _cts?.Cancel(); _cts?.Dispose();
    }
}
