using System.Threading.Channels;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using ArbitrageBot.Domain.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.Application.Services;

public class TradeExecutorService : BackgroundService, ITradeExecutor
{
    private readonly IArbitrageDetector _detector;
    private readonly IWalletManager _walletManager;
    private readonly ITradeRepository _tradeRepository;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ProfitCalculator _calculator;
    private readonly IOrderBookAggregator _cache;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<TradeExecutorService> _logger;
    private readonly Channel<TradeResult> _outputChannel;
    private readonly Random _rng = new();
    private readonly int _networkLatencyMs;

    public TradeExecutorService(
        IArbitrageDetector detector, IWalletManager walletManager,
        ITradeRepository tradeRepository, CircuitBreaker circuitBreaker,
        ProfitCalculator calculator, IOrderBookAggregator cache,
        IOptions<ArbitrageOptions> arbOpts,
        IHubContext<ArbitrageHub> hubContext, ILogger<TradeExecutorService> logger)
    {
        _detector = detector; _walletManager = walletManager;
        _tradeRepository = tradeRepository; _circuitBreaker = circuitBreaker;
        _calculator = calculator; _cache = cache; _hubContext = hubContext;
        _logger = logger;
        _networkLatencyMs = arbOpts.Value.NetworkLatencyMs;
        _outputChannel = Channel.CreateUnbounded<TradeResult>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    }

    public ChannelReader<TradeResult> GetReader() => _outputChannel.Reader;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("TradeExecutor iniciado (latencia simulada: {L}ms)", _networkLatencyMs);
        var reader = _detector.GetReader();

        try
        {
            await foreach (var opp in reader.ReadAllAsync(ct))
            {
                if (!_calculator.IsExecutable(opp)) continue;

                var result = await ExecuteTradeAsync(opp, ct);
                await _outputChannel.Writer.WriteAsync(result, ct);
                _circuitBreaker.RecordTrade(result.IsProfit);
                await _hubContext.Clients.All.SendAsync("TradeExecuted", result, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "TradeExecutor fatal"); throw; }
        finally { _outputChannel.Writer.TryComplete(); }
    }

    private async Task<TradeResult> ExecuteTradeAsync(ArbitrageOpportunity opp, CancellationToken ct)
    {
        if (_circuitBreaker.IsOpen)
            return await SaveAndReturn(opp, 0, false, "circuit_open", ct);

        // ─── Simular latencia de red ────────────────────────────────
        var netLatency = _networkLatencyMs + _rng.Next(-_networkLatencyMs / 2, _networkLatencyMs / 2);
        if (netLatency > 0) await Task.Delay(Math.Max(netLatency, 0), ct);

        // ─── Re-evaluar precio después de la latencia simulada ────
        var bookBuy = _cache.GetLatest(opp.BuyExchange);
        var bookSell = _cache.GetLatest(opp.SellExchange);
        if (bookBuy == null || bookSell == null)
            return await SaveAndReturn(opp, 0, false, "stale_prices", ct);

        var currentOpp = _calculator.Evaluate(bookBuy, bookSell);
        if (!_calculator.IsExecutable(currentOpp))
        {
            _logger.LogWarning("Oportunidad expiró tras latencia de {L}ms — {Buy}→{Sell} ya no es rentable",
                netLatency, opp.BuyExchange, opp.SellExchange);
            return await SaveAndReturn(opp, 0, false, "price_moved", ct);
        }

        // ─── Verificar fondos ──────────────────────────────────────
        var buyBal = _walletManager.GetBalance(opp.BuyExchange);
        var sellBal = _walletManager.GetBalance(opp.SellExchange);
        var maxByUsdt = buyBal.UsdtBalance / (opp.AskPrice > 0 ? opp.AskPrice : 1);
        var maxByBtc = sellBal.BtcBalance;
        var execVol = Math.Min(Math.Min(opp.Volume, maxByUsdt), maxByBtc);

        if (execVol <= 0)
            return await SaveAndReturn(opp, 0, false, "insufficient_funds", ct);

        var usdtNeeded = opp.AskPrice * execVol;
        var usdtEarned = opp.BidPrice * execVol;

        if (!_walletManager.TryExecuteBuy(opp.BuyExchange, usdtNeeded, execVol) ||
            !_walletManager.TryExecuteSell(opp.SellExchange, execVol, usdtEarned))
            return await SaveAndReturn(opp, 0, false, "insufficient_funds", ct);

        var actualProfit = opp.NetProfit * (execVol / opp.Volume);
        var status = execVol < opp.Volume ? "executed_partial" : "executed";

        _logger.LogInformation("[✓] {B}→{S} Vol={V:F4} Net={P:F3} Lat={L}ms",
            opp.BuyExchange, opp.SellExchange, execVol, actualProfit, netLatency);

        return await SaveAndReturn(opp, actualProfit, actualProfit > 0, status, ct);
    }

    private async Task<TradeResult> SaveAndReturn(ArbitrageOpportunity opp, decimal netProfit,
        bool isProfit, string status, CancellationToken ct)
    {
        var t = new TradeResult(Guid.NewGuid(), opp.BuyExchange, opp.SellExchange, opp.Volume,
            isProfit ? netProfit : 0, isProfit ? opp.ReturnPct : 0, isProfit, status, DateTime.UtcNow);
        await _tradeRepository.SaveAsync(t, ct);
        return t;
    }
}
