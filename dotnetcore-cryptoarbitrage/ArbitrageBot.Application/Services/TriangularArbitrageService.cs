using System.Net.Http.Json;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Detecta oportunidades de arbitraje triangular dentro de un mismo exchange.
/// Ruta clasica: BTC → USDT → ETH → BTC
/// 
/// Usa REST polling para obtener precio ETH/USDT de los 3 exchanges principales
/// y los combina con los order books BTC/USDT en vivo.
/// 
/// Solo evalua si el profit neto supera 0.15% (umbral mas bajo que pairwise
/// porque no hay withdrawal fee entre exchanges).
/// </summary>
public class TriangularArbitrageService : BackgroundService
{
    private readonly IOrderBookAggregator _cache;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<TriangularArbitrageService> _logger;
    private readonly HttpClient _httpClient;

    // Umbrales para triangular: sin withdrawal fee, solo trading fees
    private const decimal MinReturnPct = 0.0015m;      // 0.15%
    private const decimal TradingFee = 0.001m;           // 0.1% por trade
    private const decimal StartBtcAmount = 1.0m;

    // Endpoints REST para precio ETH/USDT (rapido, sin WS adicional)
    private static readonly Dictionary<string, string> EthEndpoints = new()
    {
        ["Binance"] = "https://api.binance.com/api/v3/ticker/bookTicker?symbol=ETHUSDT",
        ["Kraken"] = "https://api.kraken.com/0/public/Ticker?pair=ETHUSDT",
        ["Bybit"] = "https://api.bybit.com/v5/market/ticker?category=spot&symbol=ETHUSDT",
    };

    public TriangularArbitrageService(
        IOrderBookAggregator cache,
        IHubContext<ArbitrageHub> hubContext,
        ILogger<TriangularArbitrageService> logger,
        HttpClient httpClient)
    {
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("TriangularArbitrageService iniciado — evaluando BTC→USDT→ETH→BTC");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var exchangeId in EthEndpoints.Keys)
                {
                    var ethPrice = await FetchEthPriceAsync(exchangeId, ct);
                    if (ethPrice is null) continue;

                    var btcBook = _cache.GetLatest(exchangeId);
                    if (btcBook is null) continue;

                    var opp = EvaluateTriangular(exchangeId, btcBook, ethPrice.Value);
                    if (opp is not null)
                    {
                        _logger.LogInformation(
                            "[TRIANGULAR ✓] {Exch} {Path} | Start={S:F6} End={E:F6} | Net={N:F6} BTC | Ret={R:P2}",
                            opp.ExchangeId, opp.Path, opp.StartAmountBtc,
                            opp.EndAmountBtc, opp.NetProfitBtc, opp.ReturnPct);

                        // Fire & forget SignalR
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _hubContext.Clients.All.SendAsync(
                                    "TriangularOpportunity", opp, ct);
                            }
                            catch { }
                        }, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en ciclo de arbitraje triangular");
            }

            await Task.Delay(2000, ct); // Evaluar cada 2 segundos
        }
    }

    /// <summary>
    /// Evalua la ruta triangular BTC → USDT → ETH → BTC.
    /// 
    /// Paso 1: Vender BTC por USDT (usando BestBid del order book BTC/USDT)
    /// Paso 2: Comprar ETH con USDT (usando Ask de ETH/USDT)
    /// Paso 3: Vender ETH por BTC (usando... necesitamos ETH/BTC)
    /// 
    /// Simplificacion: asumimos que ETH/BTC = (ETH/USDT) / (BTC/USDT)
    /// Esta es una aproximacion valida porque los libros estan acoplados.
    /// </summary>
    private TriangularOpportunity? EvaluateTriangular(
        string exchangeId, OrderBook btcBook, decimal ethPrice)
    {
        // -- Paso 1: BTC → USDT (vender BTC al bid) --
        var btcUsdtBid = btcBook.BestBid;
        var usdtFromBtc = StartBtcAmount * btcUsdtBid * (1 - TradingFee);

        // -- Paso 2: USDT → ETH (comprar ETH al ask) --
        // ethAsk = mejor precio para comprar ETH con USDT
        var ethAsk = ethPrice; // Simplificacion: usamos el mid-price del ticker
        var ethAmount = usdtFromBtc / ethAsk * (1 - TradingFee);

        // -- Paso 3: ETH → BTC (vender ETH al bid) --
        // Aproximacion: ETH/BTC = ETH_USDT / BTC_USDT (asumiendo arbitraje acoplado)
        var btcUsdtAsk = btcBook.BestAsk;
        var ethBtcBid = ethAsk / btcUsdtAsk; // Cuantos BTC por 1 ETH al bid
        var btcFromEth = ethAmount * ethBtcBid * (1 - TradingFee);

        // -- Resultado --
        var netProfitBtc = btcFromEth - StartBtcAmount;
        var returnPct = netProfitBtc / StartBtcAmount;

        if (returnPct <= MinReturnPct) return null;

        return new TriangularOpportunity(
            ExchangeId: exchangeId,
            Path: "BTC→USDT→ETH→BTC",
            StartAmountBtc: StartBtcAmount,
            EndAmountBtc: btcFromEth,
            NetProfitBtc: netProfitBtc,
            ReturnPct: returnPct,
            StepRates: new[] { btcUsdtBid, ethAsk, ethBtcBid },
            DetectedAt: DateTime.UtcNow);
    }

    private async Task<decimal?> FetchEthPriceAsync(string exchangeId, CancellationToken ct)
    {
        try
        {
            var url = EthEndpoints[exchangeId];
            var response = await _httpClient.GetStringAsync(url, ct);

            return exchangeId switch
            {
                "Binance" => ParseBinanceEth(response),
                "Kraken" => ParseKrakenEth(response),
                "Bybit" => ParseBybitEth(response),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{Exch}] Error obteniendo ETH/USDT", exchangeId);
            return null;
        }
    }

    private static decimal? ParseBinanceEth(string raw)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(raw);
            var bid = json.RootElement.GetProperty("bidPrice").GetDecimal();
            var ask = json.RootElement.GetProperty("askPrice").GetDecimal();
            return (bid + ask) / 2m; // mid-price
        }
        catch { return null; }
    }

    private static decimal? ParseKrakenEth(string raw)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(raw);
            var result = json.RootElement.GetProperty("result");
            // Kraken usa el par como key: "XETHZUSD"
            foreach (var kv in result.EnumerateObject())
            {
                var ticker = kv.Value;
                var bid = ticker.GetProperty("b")[0].GetDecimal();
                var ask = ticker.GetProperty("a")[0].GetDecimal();
                return (bid + ask) / 2m;
            }
            return null;
        }
        catch { return null; }
    }

    private static decimal? ParseBybitEth(string raw)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(raw);
            var result = json.RootElement.GetProperty("result");
            var list = result.GetProperty("list");
            var ticker = list[0];
            var bid = ticker.GetProperty("bid1Price").GetDecimal();
            var ask = ticker.GetProperty("ask1Price").GetDecimal();
            return (bid + ask) / 2m;
        }
        catch { return null; }
    }
}
