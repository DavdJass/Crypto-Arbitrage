using ArbitrageBot.Domain.Configuration;using ArbitrageBot.Domain.Interfaces;using ArbitrageBot.Domain.Models;using Microsoft.Extensions.Logging;using Microsoft.Extensions.Options;using Newtonsoft.Json.Linq;

namespace ArbitrageBot.Infrastructure.Feeds;

public sealed class KuCoinFeed : IExchangeFeed, IDisposable
{
    private readonly ILogger<KuCoinFeed> _l;private readonly HttpClient _hc;private readonly FeedHealthTracker _h;
    private readonly string _symbol,_restUrl;private CancellationTokenSource? _cts;private bool _d;
    public string ExchangeId=>"KuCoin";public event Action<OrderBook>? OnOrderBookUpdated;

    public KuCoinFeed(ILogger<KuCoinFeed> l,HttpClient hc,FeedHealthTracker h,IOptions<ExchangeOptions> o)
    {_l=l;_hc=hc;_h=h;var c=o.Value.Exchanges["KuCoin"];_symbol=c.Symbol;_restUrl=c.RestUrl;}

    public async Task ConnectAsync(CancellationToken ct)
    {_cts=CancellationTokenSource.CreateLinkedTokenSource(ct);_h.SetStatus(ExchangeId,"fallback_rest","REST polling");
     _l.LogInformation("[{X}] Usando REST polling (WS requiere token dinámico)",ExchangeId);
     _=Task.Run(()=>RestLoop(_cts.Token),ct);await Task.CompletedTask;}

    async Task RestLoop(CancellationToken ct)
    {while(!ct.IsCancellationRequested)
     {try{var r=await _hc.GetStringAsync($"{_restUrl}{_symbol}",ct);var j=JObject.Parse(r);var d=j["data"];
      if(d!=null){var ob=new OrderBook(ExchangeId,(decimal)d["bestBid"],(decimal)d["bestAsk"],
        (decimal)(d["bestBidSize"]??0),(decimal)(d["bestAskSize"]??0),DateTime.UtcNow);
        OnOrderBookUpdated?.Invoke(ob);_h.SetStatus(ExchangeId,"fallback_rest","OK");}}
      catch(Exception ex){_l.LogWarning(ex,"[KuCoin] REST error");_h.SetStatus(ExchangeId,"fallback_rest","Error");}
      await Task.Delay(1500,ct);}}

    public async Task DisconnectAsync(){_cts?.Cancel();}
    public void Dispose(){if(_d)return;_d=true;_cts?.Cancel();_cts?.Dispose();}
}
