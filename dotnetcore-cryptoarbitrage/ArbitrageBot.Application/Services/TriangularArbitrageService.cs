using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Detecta oportunidades de arbitraje triangular dentro de un mismo exchange.
/// Ruta: BTC → USDT → ETH → BTC
/// 
/// Usa REST polling para obtener precio ETH/USDT de los 3 exchanges principales
/// y los combina con los order books BTC/USDT en vivo.
/// 
/// Solo evalua si el profit neto supera MinReturnPct (umbral más bajo que pairwise
/// porque no hay withdrawal fee entre exchanges).
/// </summary>
public class TriangularArbitrageService : BackgroundService
{
    private readonly IOrderBookAggregator _cache;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<TriangularArbitrageService> _logger;
    private readonly HttpClient _httpClient;

    private const decimal MinReturnPct = 0.0015m;
    private const decimal TradingFee = 0.001m;
    private const decimal StartBtcAmount = 1.0m;
    private const decimal AskSlippage = 0.0003m; // proxy conservador para el ask de ETH/USDT vs mid

    private static readonly Dictionary<string, string> EthEndpoints = new()
    {
        ["Binance"] = "https://api.binance.com/api/v3/ticker/bookTicker?symbol=ETHUSDT",
        ["Kraken"]  = "https://api.kraken.com/0/public/Ticker?pair=ETHUSDT",
        ["Bybit"]   = "https://api.bybit.com/v5/market/ticker?category=spot&symbol=ETHUSDT",
    };

    public TriangularArbitrageService(
        IOrderBookAggregator cache,
        CircuitBreaker circuitBreaker,
        IHubContext<ArbitrageHub> hubContext,
        ILogger<TriangularArbitrageService> logger,
        HttpClient httpClient)
    {
        _cache = cache;
        _circuitBreaker = circuitBreaker;
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
                if (!_circuitBreaker.IsOpen)
                {
                    foreach (var exchangeId in EthEndpoints.Keys)
                    {
                        var ethPrices = await FetchEthPricesAsync(exchangeId, ct);
                        if (ethPrices is null) continue;

                        var btcBook = _cache.GetLatest(exchangeId);
                        if (btcBook is null || btcBook.BestBid <= 0 || btcBook.BestAsk <= 0 || btcBook.BestAsk <= btcBook.BestBid)
                            continue;

                        var opp = EvaluateTriangular(exchangeId, btcBook, ethPrices.Value.bid, ethPrices.Value.ask);
                        if (opp is not null)
                        {
                            _logger.LogInformation(
                                "[TRIANGULAR ✓] {Exch} {Path} | Start={S:F6} End={E:F6} | Net={N:F6} BTC | Ret={R:P2}",
                                opp.ExchangeId, opp.Path, opp.StartAmountBtc,
                                opp.EndAmountBtc, opp.NetProfitBtc, opp.ReturnPct);

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _hubContext.Clients.All.SendAsync("TriangularOpportunity", opp, ct);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "[{Exch}] Error SignalR TriangularOpportunity", exchangeId);
                                }
                            }, ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en ciclo de arbitraje triangular");
            }

            await Task.Delay(2000, ct);
        }
    }

    /// <summary>
    /// Evalua la ruta triangular BTC → USDT → ETH → BTC.
    /// Paso 1: Vender BTC → USDT al bid del book.
    /// Paso 2: Comprar ETH con USDT al ask real de ETH/USDT.
    /// Paso 3: Vender ETH → BTC implícito via (ETH_bid / BTC_ask).
    /// </summary>
    private static TriangularOpportunity? EvaluateTriangular(
        string exchangeId, OrderBook btcBook, decimal ethBid, decimal ethAsk)
    {
        var btcUsdtBid = btcBook.BestBid;
        var btcUsdtAsk = btcBook.BestAsk;

        // Paso 1: BTC → USDT (vender BTC al bid)
        var usdtFromBtc = StartBtcAmount * btcUsdtBid * (1 - TradingFee);

        // Paso 2: USDT → ETH (comprar ETH al ask real)
        var ethAmount = usdtFromBtc / ethAsk * (1 - TradingFee);

        // Paso 3: ETH → BTC implícito: bid de ETH sobre ask de BTC
        var ethBtcBid = ethBid / btcUsdtAsk;
        var btcFromEth = ethAmount * ethBtcBid * (1 - TradingFee);

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

    private async Task<(decimal bid, decimal ask)?> FetchEthPricesAsync(string exchangeId, CancellationToken ct)
    {
        try
        {
            var url = EthEndpoints[exchangeId];
            var response = await _httpClient.GetStringAsync(url, ct);

            return exchangeId switch
            {
                "Binance" => ParseBinanceEth(response),
                "Kraken"  => ParseKrakenEth(response),
                "Bybit"   => ParseBybitEth(response),
                _         => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{Exch}] Error obteniendo ETH/USDT", exchangeId);
            return null;
        }
    }

    private static (decimal bid, decimal ask)? ParseBinanceEth(string raw)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var bid = doc.RootElement.GetProperty("bidPrice").GetDecimal();
            var ask = doc.RootElement.GetProperty("askPrice").GetDecimal();
            return bid > 0 && ask > 0 ? (bid, ask) : null;
        }
        catch { return null; }
    }

    private static (decimal bid, decimal ask)? ParseKrakenEth(string raw)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var result = doc.RootElement.GetProperty("result");
            foreach (var kv in result.EnumerateObject())
            {
                var ticker = kv.Value;
                var bid = ticker.GetProperty("b")[0].GetDecimal();
                var ask = ticker.GetProperty("a")[0].GetDecimal();
                return bid > 0 && ask > 0 ? (bid, ask) : null;
            }
            return null;
        }
        catch { return null; }
    }

    private static (decimal bid, decimal ask)? ParseBybitEth(string raw)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var list = doc.RootElement.GetProperty("result").GetProperty("list");
            var ticker = list[0];
            var bid = ticker.GetProperty("bid1Price").GetDecimal();
            var ask = ticker.GetProperty("ask1Price").GetDecimal();
            return bid > 0 && ask > 0 ? (bid, ask) : null;
        }
        catch { return null; }
    }
}
