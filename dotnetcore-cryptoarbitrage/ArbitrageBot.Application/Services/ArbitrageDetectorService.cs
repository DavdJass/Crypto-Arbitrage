using System.Threading.Channels;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// BackgroundService que lee del Channel&lt;OrderBook&gt; y evalúa todas las
/// combinaciones N×N de exchanges. Las oportunidades detectadas se priorizan
/// por NetProfit descendente. Emite vía Channel y SignalR (fire-and-forget).
/// </summary>
public class ArbitrageDetectorService : BackgroundService, IArbitrageDetector
{
    private readonly IOrderBookAggregator _cache;
    private readonly ProfitCalculator _calculator;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<ArbitrageDetectorService> _logger;
    private readonly Channel<ArbitrageOpportunity> _outputChannel;

    public ArbitrageDetectorService(
        IOrderBookAggregator cache,
        ProfitCalculator calculator,
        CircuitBreaker circuitBreaker,
        IHubContext<ArbitrageHub> hubContext,
        ILogger<ArbitrageDetectorService> logger)
    {
        _cache = cache;
        _calculator = calculator;
        _circuitBreaker = circuitBreaker;
        _hubContext = hubContext;
        _logger = logger;
        _outputChannel = Channel.CreateUnbounded<ArbitrageOpportunity>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    }

    public ChannelReader<ArbitrageOpportunity> GetReader() => _outputChannel.Reader;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ArbitrageDetectorService iniciado");
        var reader = _cache.GetReader();

        try
        {
            await foreach (var orderBook in reader.ReadAllAsync(stoppingToken))
            {
                if (_circuitBreaker.IsOpen)
                {
                    _logger.LogDebug("Circuito abierto — omitiendo detección");
                    continue;
                }

                var opportunities = EvaluateAllPairs(orderBook.ExchangeId);

                // ─── Priorizar por NetProfit descendente ─────────────────
                opportunities.Sort((a, b) => b.NetProfit.CompareTo(a.NetProfit));

                foreach (var opp in opportunities)
                {
                    // Emitir al pipeline (Channel) — la mejor primero
                    await _outputChannel.Writer.WriteAsync(opp, stoppingToken);

                    // Push a SignalR — FIRE AND FORGET (no bloquea el pipeline)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _hubContext.Clients.All
                                .SendAsync("OpportunityFound", opp, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error SignalR OpportunityFound");
                        }
                    }, stoppingToken);

                    if (_calculator.IsExecutable(opp))
                    {
                        _logger.LogInformation(
                            "[OPORTUNIDAD ✓] Buy={Buy}@{Ask:F2} → Sell={Sell}@{Bid:F2} | Net={Net:F3} | Ret={Ret:P2}",
                            opp.BuyExchange, opp.AskPrice, opp.SellExchange, opp.BidPrice,
                            opp.NetProfit, opp.ReturnPct);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArbitrageDetectorService error fatal");
            throw;
        }
        finally
        {
            _outputChannel.Writer.TryComplete();
            _logger.LogInformation("ArbitrageDetectorService finalizado");
        }
    }

    private List<ArbitrageOpportunity> EvaluateAllPairs(string updatedExchangeId)
    {
        var opportunities = new List<ArbitrageOpportunity>();
        var allBooks = _cache.GetAllOrderBooks();

        if (allBooks.Count < 2) return opportunities;

        var exchangeIds = allBooks.Keys.ToList();

        for (int i = 0; i < exchangeIds.Count; i++)
        {
            for (int j = 0; j < exchangeIds.Count; j++)
            {
                if (i == j) continue;
                var opp = _calculator.Evaluate(
                    allBooks[exchangeIds[i]],
                    allBooks[exchangeIds[j]]);
                opportunities.Add(opp);
            }
        }

        return opportunities;
    }
}
