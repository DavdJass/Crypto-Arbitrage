using System.Net.WebSockets;using System.Text;using ArbitrageBot.Domain.Configuration;using ArbitrageBot.Domain.Interfaces;using ArbitrageBot.Domain.Models;using Microsoft.Extensions.Logging;using Microsoft.Extensions.Options;using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

public sealed class BitfinexFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<BitfinexFeed> _l;private readonly HttpClient _hc;private readonly FeedHealthTracker _h;
    private readonly string _ws,_rest;private ClientWebSocket? _wsCli;private CancellationTokenSource? _cts;
    private bool _d,_fallback;
    public string ExchangeId => "Bitfinex";public event Action<OrderBook>? OnOrderBookUpdated;

    public BitfinexFeed(ILogger<BitfinexFeed> l,HttpClient hc,FeedHealthTracker h,IOptions<ExchangeOptions> o)
    {_l=l;_hc=hc;_h=h;var c=o.Value.Exchanges["Bitfinex"];_ws=c.WebSocketUrl;_rest=c.RestUrl;}

    public async Task ConnectAsync(CancellationToken ct){_cts=CancellationTokenSource.CreateLinkedTokenSource(ct);_h.SetStatus(ExchangeId,"connecting");_=Task.Run(()=>ConnectLoop(_cts.Token),ct);await Task.CompletedTask;}

    async Task ConnectLoop(CancellationToken ct){var d=2;while(!ct.IsCancellationRequested){try{_wsCli?.Dispose();_wsCli=new();await _wsCli.ConnectAsync(new(_ws),ct);var sub=Encoding.UTF8.GetBytes("{\"event\":\"subscribe\",\"channel\":\"book\",\"symbol\":\"tBTCUSD\",\"prec\":\"P0\",\"freq\":\"F0\",\"len\":\"1\"}");await _wsCli.SendAsync(sub,WebSocketMessageType.Text,true,ct);_h.SetStatus(ExchangeId,"connected","WS");_fallback=false;_l.LogInformation("[{X}] WS conectado",ExchangeId);d=2;await ReceiveLoop(ct);}catch(OperationCanceledException){break;}catch(Exception ex){_l.LogError(ex,"[{X}] WS error",ExchangeId);_fallback=true;await RestLoop(ct);await Task.Delay(TimeSpan.FromSeconds(Math.Min(d,30)),ct);d*=2;}}}

    async Task ReceiveLoop(CancellationToken ct){var buf=new byte[32768];var sb=new StringBuilder();while(_wsCli?.State==WebSocketState.Open&&!ct.IsCancellationRequested){sb.Clear();WebSocketReceiveResult r;do{r=await _wsCli.ReceiveAsync(buf,ct);sb.Append(Encoding.UTF8.GetString(buf,0,r.Count));}while(!r.EndOfMessage);if(r.MessageType==WebSocketMessageType.Close)break;try{var ob=Parse(sb.ToString());if(ob!=null){OnOrderBookUpdated?.Invoke(ob);_h.SetStatus(ExchangeId,"connected","WS");}}catch{}}}

    async Task RestLoop(CancellationToken ct){while(_fallback&&!ct.IsCancellationRequested){try{var r=await _hc.GetStringAsync(_rest,ct);var ob=ParseRest(r);if(ob!=null){OnOrderBookUpdated?.Invoke(ob);_h.SetStatus(ExchangeId,"fallback_rest","REST");}}catch{}await Task.Delay(2000,ct);}}

    OrderBook? Parse(string raw){try{var j=JToken.Parse(raw);if(j is JArray a&&a.Count>1&&a[1] is JArray d){if(a[0]?.ToString()=="hb")return null;if(d.Count>=3&&d[0] is JArray bid&&d[2] is JArray ask)return new(ExchangeId,(decimal)bid[0],(decimal)ask[0],(decimal)bid[1],(decimal)ask[1],DateTime.UtcNow);}}catch{}return null;}

    OrderBook? ParseRest(string raw){try{var j=JObject.Parse(raw);return new(ExchangeId,(decimal)j["bid"],(decimal)j["ask"],(decimal)(j["bid_size"]??0),(decimal)(j["ask_size"]??0),DateTime.UtcNow);}catch{return null;}}

    public async Task DisconnectAsync(){_fallback=false;_cts?.Cancel();if(_wsCli?.State==WebSocketState.Open)await _wsCli.CloseAsync(WebSocketCloseStatus.NormalClosure,"",CancellationToken.None);}
    public void Dispose(){if(_d)return;_d=true;_wsCli?.Dispose();_cts?.Cancel();_cts?.Dispose();}
}
