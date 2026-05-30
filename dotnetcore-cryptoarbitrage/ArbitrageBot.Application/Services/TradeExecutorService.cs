using System.Threading.Channels;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using ArbitrageBot.Application.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// BackgroundService que consume Channel&lt;ArbitrageOpportunity&gt;,
/// filtra por rentabilidad, calcula volumen máximo por liquidez,
/// simula ejecución con órdenes parciales, actualiza wallets y pushea via SignalR.
/// </summary>
public class TradeExecutorService : BackgroundService, ITradeExecutor
{
    private readonly IArbitrageDetector _detector;
    private readonly IWalletManager _walletManager;
    private readonly ITradeRepository _tradeRepository;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ProfitCalculator _calculator;
    private readonly IHubContext<ArbitrageHub> _hubContext;
    private readonly ILogger<TradeExecutorService> _logger;
    private readonly Channel<TradeResult> _outputChannel;

    public TradeExecutorService(
        IArbitrageDetector detector,
        IWalletManager walletManager,
        ITradeRepository tradeRepository,
        CircuitBreaker circuitBreaker,
        ProfitCalculator calculator,
        IHubContext<ArbitrageHub> hubContext,
        ILogger<TradeExecutorService> logger)
    {
        _detector = detector;
        _walletManager = walletManager;
        _tradeRepository = tradeRepository;
        _circuitBreaker = circuitBreaker;
        _calculator = calculator;
        _hubContext = hubContext;
        _logger = logger;
        _outputChannel = Channel.CreateUnbounded<TradeResult>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    }

    public ChannelReader<TradeResult> GetReader() => _outputChannel.Reader;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradeExecutorService iniciado");
        var reader = _detector.GetReader();

        try
        {
            await foreach (var opportunity in reader.ReadAllAsync(stoppingToken))
            {
                if (!_calculator.IsExecutable(opportunity))
                {
                    _logger.LogTrace("Oportunidad no ejecutable — omitida: {Buy}→{Sell}",
                        opportunity.BuyExchange, opportunity.SellExchange);
                    continue;
                }

                var result = await ExecuteTradeAsync(opportunity, stoppingToken);
                await _outputChannel.Writer.WriteAsync(result, stoppingToken);
                _circuitBreaker.RecordTrade(result.IsProfit);

                // Push via SignalR
                await _hubContext.Clients.All.SendAsync("TradeExecuted", result, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TradeExecutorService error fatal");
            throw;
        }
        finally
        {
            _outputChannel.Writer.TryComplete();
            _logger.LogInformation("TradeExecutorService finalizado");
        }
    }

    private async Task<TradeResult> ExecuteTradeAsync(ArbitrageOpportunity opp, CancellationToken ct)
    {
        if (_circuitBreaker.IsOpen)
        {
            var result = CreateTradeResult(opp, 0, false, "circuit_open");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        // ─── Obtener OrderBooks actuales para liquidez real ────────────
        var buyBalance = _walletManager.GetBalance(opp.BuyExchange);
        var sellBalance = _walletManager.GetBalance(opp.SellExchange);

        // Volumen máximo por cap de riesgo
        var maxVolume = opp.Volume;

        // Ajustar por liquidez de wallets
        var maxByUsdt = buyBalance.UsdtBalance / (opp.AskPrice > 0 ? opp.AskPrice : 1);
        var maxByBtc = sellBalance.BtcBalance;

        var executableVolume = Math.Min(Math.Min(maxVolume, maxByUsdt), maxByBtc);

        if (executableVolume <= 0)
        {
            _logger.LogWarning(
                "Fondos insuficientes para ejecutar {Buy}→{Sell}. USDT:{U} BTC:{B}",
                opp.BuyExchange, opp.SellExchange, maxByUsdt, maxByBtc);
            var result = CreateTradeResult(opp, 0, false, "insufficient_funds");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        var usdtNeeded = opp.AskPrice * executableVolume;
        var usdtEarned = opp.BidPrice * executableVolume;

        // ─── Ejecutar compra y venta ────────────────────────────────────
        var buyOk = _walletManager.TryExecuteBuy(opp.BuyExchange, usdtNeeded, executableVolume);
        var sellOk = _walletManager.TryExecuteSell(opp.SellExchange, executableVolume, usdtEarned);

        if (!buyOk || !sellOk)
        {
            _logger.LogWarning("Falló ejecución atómica del trade {Buy}→{Sell}",
                opp.BuyExchange, opp.SellExchange);
            var result = CreateTradeResult(opp, 0, false, "insufficient_funds");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        // Calcular profit real basado en el volumen parcial ejecutado
        var actualNetProfit = opp.NetProfit * (executableVolume / opp.Volume);
        var actualReturnPct = opp.ReturnPct; // mismo % porque es proporcional

        var tradeResult = CreateTradeResult(opp, actualNetProfit, actualNetProfit > 0,
            executableVolume < opp.Volume ? "executed_partial" : "executed");

        await _tradeRepository.SaveAsync(tradeResult, ct);

        _logger.LogInformation(
            "[EJECUTADO ✓] {Buy}→{Sell} | Vol={Vol:F4}BTC | Net={Net:F3}USDT | Ret={Ret:P2}",
            opp.BuyExchange, opp.SellExchange, executableVolume,
            actualNetProfit, actualReturnPct);

        return tradeResult;
    }

    private static TradeResult CreateTradeResult(
        ArbitrageOpportunity opp, decimal netProfit, bool isProfit, string status)
    {
        return new TradeResult(
            Id: Guid.NewGuid(),
            BuyExchange: opp.BuyExchange,
            SellExchange: opp.SellExchange,
            Volume: opp.Volume,
            NetProfit: isProfit ? netProfit : 0,
            ReturnPct: isProfit ? opp.ReturnPct : 0,
            IsProfit: isProfit,
            Status: status,
            ExecutedAt: DateTime.UtcNow
        );
    }
}
