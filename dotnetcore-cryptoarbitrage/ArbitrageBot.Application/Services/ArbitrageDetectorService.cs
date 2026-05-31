using System.Collections.Concurrent;
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
/// combinaciones N×N de exchanges.
///
/// Throttling por par:
///   - "detected" (ejecutable): emite inmediato, sin cooldown.
///   - "observed"  (spread positivo pero bajo umbral): emite como máximo
///     una vez cada ObservedThrottleSeconds por par, para no saturar SignalR.
///
/// Además, en cada ciclo sólo se persiste el top-K por spread para no
/// llenar el repositorio con miles de entradas "observed" idénticas.
/// </summary>
public class ArbitrageDetectorService : BackgroundService, IArbitrageDetector
{
    private const int ObservedThrottleSeconds = 5;
    private const int MaxObservedPerCycle = 5; // sólo los mejores "observed" se emiten

    private readonly IOrderBookAggregator _cache;
    private readonly ProfitCalculator _calculator;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<ArbitrageDetectorService> _logger;
    private readonly Channel<ArbitrageOpportunity> _outputChannel;

    // Rastrea cuándo se emitió por última vez cada par "observed"
    private readonly ConcurrentDictionary<string, DateTime> _observedThrottle = new();

    public ArbitrageDetectorService(
        IOrderBookAggregator cache,
        ProfitCalculator calculator,
        CircuitBreaker circuitBreaker,
        IOpportunityRepository opportunityRepository,
        IHubContext<ArbitrageHub> hubContext,
        ILogger<ArbitrageDetectorService> logger)
    {
        _cache = cache;
        _calculator = calculator;
        _circuitBreaker = circuitBreaker;
        _opportunityRepository = opportunityRepository;
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
                opportunities.Sort((a, b) => b.NetProfit.CompareTo(a.NetProfit));

                int observedEmitted = 0;

                foreach (var opp in opportunities)
                {
                    if (!ProfitCalculator.HasPositiveSpread(opp)) continue;

                    var isExecutable = _calculator.IsExecutable(opp);
                    var pairKey = $"{opp.BuyExchange}→{opp.SellExchange}";

                    // Throttle para "observed": máx 1 emisión por par cada N segundos
                    if (!isExecutable)
                    {
                        if (observedEmitted >= MaxObservedPerCycle) continue;

                        if (_observedThrottle.TryGetValue(pairKey, out var lastEmit) &&
                            (DateTime.UtcNow - lastEmit).TotalSeconds < ObservedThrottleSeconds)
                            continue;

                        _observedThrottle[pairKey] = DateTime.UtcNow;
                        observedEmitted++;
                    }

                    var status = isExecutable ? "detected" : "observed";
                    var stored = new StoredOpportunity(
                        Guid.NewGuid(),
                        opp.BuyExchange,
                        opp.SellExchange,
                        opp.AskPrice,
                        opp.BidPrice,
                        opp.Volume,
                        opp.NetProfit,
                        opp.ReturnPct,
                        status,
                        isExecutable ? null : "spread_bajo_umbral",
                        opp.DetectedAt);

                    await _opportunityRepository.SaveAsync(stored, stoppingToken);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _hubContext.Clients.All
                                .SendAsync("OpportunityFound", stored, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error SignalR OpportunityFound [{Pair}]", pairKey);
                        }
                    }, stoppingToken);

                    if (isExecutable)
                    {
                        await _outputChannel.Writer.WriteAsync(opp, stoppingToken);
                        _logger.LogInformation(
                            "[OPORTUNIDAD ✓] {Pair} | Net={Net:F3} | Ret={Ret:P2}",
                            pairKey, opp.NetProfit, opp.ReturnPct);
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
