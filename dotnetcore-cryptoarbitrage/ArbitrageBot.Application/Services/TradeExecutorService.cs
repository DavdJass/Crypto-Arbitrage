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
/// filtra por rentabilidad (IsExecutable), simula la ejecución,
/// actualiza wallets, registra en DB y pushea via SignalR.
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
                // Solo ejecutar si es rentable
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
            var result = CreateTradeResult(opp, false, "circuit_open");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        // Verificar fondos en ambos exchanges
        var buyBalance = _walletManager.GetBalance(opp.BuyExchange);
        var sellBalance = _walletManager.GetBalance(opp.SellExchange);

        var usdtNeeded = opp.AskPrice * opp.Volume;
        var btcNeeded = opp.Volume;

        if (buyBalance.UsdtBalance < usdtNeeded)
        {
            _logger.LogWarning("Fondos insuficientes en {Exch}: necesita {Need} USDT, tiene {Has}",
                opp.BuyExchange, usdtNeeded, buyBalance.UsdtBalance);
            var result = CreateTradeResult(opp, false, "insufficient_funds");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        if (sellBalance.BtcBalance < btcNeeded)
        {
            _logger.LogWarning("Fondos insuficientes en {Exch}: necesita {Need} BTC, tiene {Has}",
                opp.SellExchange, btcNeeded, sellBalance.BtcBalance);
            var result = CreateTradeResult(opp, false, "insufficient_funds");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        // Ejecutar compra y venta simultáneas
        var buyOk = _walletManager.TryExecuteBuy(opp.BuyExchange, usdtNeeded, opp.Volume);
        var sellOk = _walletManager.TryExecuteSell(opp.SellExchange, opp.Volume, opp.BidPrice * opp.Volume);

        if (!buyOk || !sellOk)
        {
            _logger.LogWarning("Falló ejecución atómica del trade {Buy}→{Sell}", opp.BuyExchange, opp.SellExchange);
            var result = CreateTradeResult(opp, false, "insufficient_funds");
            await _tradeRepository.SaveAsync(result, ct);
            return result;
        }

        var tradeResult = CreateTradeResult(opp, opp.NetProfit > 0, "executed");
        await _tradeRepository.SaveAsync(tradeResult, ct);

        _logger.LogInformation(
            "[EJECUTADO ✓] {Buy}→{Sell} | Vol={Vol}BTC | Net={Net:F3} | Ret={Ret:P2}",
            opp.BuyExchange, opp.SellExchange, opp.Volume, tradeResult.NetProfit, tradeResult.ReturnPct);

        return tradeResult;
    }

    private static TradeResult CreateTradeResult(
        ArbitrageOpportunity opp, bool isProfit, string status)
    {
        return new TradeResult(
            Id: Guid.NewGuid(),
            BuyExchange: opp.BuyExchange,
            SellExchange: opp.SellExchange,
            Volume: opp.Volume,
            NetProfit: isProfit ? opp.NetProfit : 0,
            ReturnPct: isProfit ? opp.ReturnPct : 0,
            IsProfit: isProfit,
            Status: status,
            ExecutedAt: DateTime.UtcNow
        );
    }
}
