using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Calcula la rentabilidad neta de una oportunidad de arbitraje.
/// Considera: trading fees, withdrawal fees, slippage, y spread.
/// </summary>
public class ProfitCalculator
{
    private readonly ArbitrageOptions _arbOptions;
    private readonly ExchangeOptions _exchangeOptions;

    public ProfitCalculator(
        IOptions<ArbitrageOptions> arbOptions,
        IOptions<ExchangeOptions> exchangeOptions)
    {
        _arbOptions = arbOptions.Value;
        _exchangeOptions = exchangeOptions.Value;
    }

    /// <summary>
    /// Evalúa la rentabilidad neta de comprar en <paramref name="buySide"/> y vender en <paramref name="sellSide"/>.
    /// El volumen se limita por liquidez visible y el cap de riesgo.
    /// </summary>
    public ArbitrageOpportunity Evaluate(OrderBook buySide, OrderBook sellSide)
    {
        var askPrice = buySide.BestAsk;
        var bidPrice = sellSide.BestBid;
        var volume = CalculateMaxVolume(buySide, sellSide);

        if (askPrice <= 0 || bidPrice <= 0 || volume <= 0)
        {
            return new ArbitrageOpportunity(
                buySide.ExchangeId, sellSide.ExchangeId,
                askPrice, bidPrice, volume, 0, 0, DateTime.UtcNow);
        }

        var settlement = ComputeSettlement(
            buySide.ExchangeId, sellSide.ExchangeId, askPrice, bidPrice, volume);

        var returnPct = settlement.BuyCostUsdt > 0
            ? settlement.NetProfit / settlement.BuyCostUsdt
            : 0;

        return new ArbitrageOpportunity(
            buySide.ExchangeId, sellSide.ExchangeId,
            askPrice, bidPrice, volume,
            settlement.NetProfit, returnPct,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Indica si la oportunidad supera el umbral mínimo para ejecutar.
    /// </summary>
    public bool IsExecutable(ArbitrageOpportunity opp)
    {
        return opp.ReturnPct > _arbOptions.MinReturnPct
               && opp.NetProfit > 0
               && opp.BidPrice > opp.AskPrice
               && opp.Volume > 0;
    }

    /// <summary>
    /// Hay spread positivo bruto (bid &gt; ask) antes de fees.
    /// </summary>
    public static bool HasPositiveSpread(ArbitrageOpportunity opp) =>
        opp.BidPrice > opp.AskPrice && opp.Volume > 0;

    /// <summary>
    /// Calcula el volumen máximo ejecutable considerando la liquidez disponible
    /// en ambos order books y el cap de riesgo.
    /// </summary>
    public decimal CalculateMaxVolume(OrderBook buySide, OrderBook sellSide)
    {
        var byCap = _arbOptions.MaxVolumeBtc;
        var byLiquidity = Math.Min(buySide.AskVolume, sellSide.BidVolume);
        return Math.Min(byCap, byLiquidity);
    }

    /// <summary>
    /// Desglose de costos alineado con wallets y ejecución simulada.
    /// </summary>
    public ExecutionSettlement ComputeSettlement(
        string buyExchange,
        string sellExchange,
        decimal askPrice,
        decimal bidPrice,
        decimal volume)
    {
        var buyCfg = GetExchangeConfig(buyExchange);
        var sellCfg = GetExchangeConfig(sellExchange);
        var tradingFeeBuy = buyCfg?.Fee ?? 0.001m;
        var tradingFeeSell = sellCfg?.Fee ?? 0.001m;
        var withdrawalFee = buyCfg?.WithdrawalFeeUsdt ?? 0m;

        var grossBuy = askPrice * volume;
        var grossSell = bidPrice * volume;
        var buyFee = grossBuy * tradingFeeBuy;
        var sellFee = grossSell * tradingFeeSell;
        var slippage = askPrice * volume * _arbOptions.SlippagePct;

        var buyCostUsdt = grossBuy + buyFee;
        var sellProceedsUsdt = grossSell - sellFee - slippage;
        var netProfit = sellProceedsUsdt - buyCostUsdt - withdrawalFee;

        return new ExecutionSettlement(
            volume,
            buyCostUsdt,
            sellProceedsUsdt,
            buyFee + sellFee,
            slippage,
            withdrawalFee,
            netProfit);
    }

    private ExchangeConfig? GetExchangeConfig(string exchangeId)
    {
        return _exchangeOptions.Exchanges.TryGetValue(exchangeId, out var cfg) ? cfg : null;
    }
}
