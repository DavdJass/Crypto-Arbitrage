using System.Net.WebSockets;using System.Text;using ArbitrageBot.Domain.Configuration;using ArbitrageBot.Domain.Interfaces;using ArbitrageBot.Domain.Models;using Microsoft.Extensions.Logging;using Microsoft.Extensions.Options;using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

public sealed class GateIoFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<GateIoFeed> _l;private readonly HttpClient _hc;private readonly FeedHealthTracker _h;
    private readonly string _ws,_rest,_symbol;private ClientWebSocket? _wsCli;private CancellationTokenSource? _cts;private bool _d,_fallback;
    public string ExchangeId=>"Gate.io";public event Action<OrderBook>? OnOrderBookUpdated;

    public GateIoFeed(ILogger<GateIoFeed> l,HttpClient hc,FeedHealthTracker h,IOptions<ExchangeOptions> o)
    {_l=l;_hc=hc;_h=h;var c=o.Value.Exchanges["Gate.io"];_ws=c.WebSocketUrl;_rest=c.RestUrl;_symbol=c.Symbol;}

    public async Task ConnectAsync(CancellationToken ct){_cts=CancellationTokenSource.CreateLinkedTokenSource(ct);_h.SetStatus(ExchangeId,"connecting");_=Task.Run(()=>ConnectLoop(_cts.Token),ct);await Task.CompletedTask;}

    async Task ConnectLoop(CancellationToken ct){var d=2;while(!ct.IsCancellationRequested){try{_wsCli?.Dispose();_wsCli=new();_wsCli.Options.KeepAliveInterval=TimeSpan.FromSeconds(30);await _wsCli.ConnectAsync(new(_ws),ct);var sub=$"{{\"time\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()},\"channel\":\"spot.order_book\",\"event\":\"subscribe\",\"payload\":[\"{_symbol}\",\"1\",\"100ms\"]}}";
    await _wsCli.SendAsync(Encoding.UTF8.GetBytes(sub),WebSocketMessageType.Text,true,ct);_h.SetStatus(ExchangeId,"connected","WS");_fallback=false;_l.LogInformation("[{X}] WS conectado",ExchangeId);d=2;await ReceiveLoop(ct);}
    catch(OperationCanceledException){break;}catch(Exception ex){_l.LogError(ex,"[{X}] WS error",ExchangeId);_fallback=true;await RestLoop(ct);await Task.Delay(TimeSpan.FromSeconds(Math.Min(d,30)),ct);d*=2;}}}

    async Task ReceiveLoop(CancellationToken ct){var buf=new byte[8192];var sb=new StringBuilder();while(_wsCli?.State==WebSocketState.Open&&!ct.IsCancellationRequested){sb.Clear();WebSocketReceiveResult r;do{r=await _wsCli.ReceiveAsync(buf,ct);sb.Append(Encoding.UTF8.GetString(buf,0,r.Count));}while(!r.EndOfMessage);if(r.MessageType==WebSocketMessageType.Close)break;try{var ob=Parse(sb.ToString());if(ob!=null){OnOrderBookUpdated?.Invoke(ob);_h.SetStatus(ExchangeId,"connected","WS");}}catch{}}}

    async Task RestLoop(CancellationToken ct){while(_fallback&&!ct.IsCancellationRequested){try{var r=await _hc.GetStringAsync(_rest,ct);var ob=ParseRest(r);if(ob!=null){OnOrderBookUpdated?.Invoke(ob);_h.SetStatus(ExchangeId,"fallback_rest","REST");}}catch{}await Task.Delay(1500,ct);}}

    OrderBook? Parse(string raw){try{var j=JObject.Parse(raw);var r=j["result"];if(r==null)return null;var bids=r["bids"]?.First;var asks=r["asks"]?.First;if(bids==null||asks==null)return null;return new(ExchangeId,(decimal)bids[0],(decimal)asks[0],(decimal)(bids[1]??0),(decimal)(asks[1]??0),DateTime.UtcNow);}catch{return null;}}

    OrderBook? ParseRest(string raw){try{var j=JObject.Parse(raw);var bids=j["bids"]?.First;var asks=j["asks"]?.First;if(bids==null||asks==null)return null;return new(ExchangeId,(decimal)bids[0],(decimal)asks[0],(decimal)(bids[1]??0),(decimal)(asks[1]??0),DateTime.UtcNow);}catch{return null;}}

    public async Task DisconnectAsync(){_fallback=false;_cts?.Cancel();if(_wsCli?.State==WebSocketState.Open)await _wsCli.CloseAsync(WebSocketCloseStatus.NormalClosure,"",CancellationToken.None);}
    public void Dispose(){if(_d)return;_d=true;_wsCli?.Dispose();_cts?.Cancel();_cts?.Dispose();}
}
