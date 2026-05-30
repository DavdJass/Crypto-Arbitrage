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
    /// </summary>
    public ArbitrageOpportunity Evaluate(OrderBook buySide, OrderBook sellSide)
    {
        var askPrice = buySide.BestAsk;
        var bidPrice = sellSide.BestBid;
        var volume = _arbOptions.MaxVolumeBtc;

        if (askPrice <= 0 || bidPrice <= 0)
        {
            return new ArbitrageOpportunity(
                buySide.ExchangeId, sellSide.ExchangeId,
                askPrice, bidPrice, volume, 0, 0, DateTime.UtcNow);
        }

        var buyCfg = GetExchangeConfig(buySide.ExchangeId);
        var sellCfg = GetExchangeConfig(sellSide.ExchangeId);
        var tradingFeeBuy = buyCfg?.Fee ?? 0.001m;
        var tradingFeeSell = sellCfg?.Fee ?? 0.001m;
        var withdrawalFee = buyCfg?.WithdrawalFeeUsdt ?? 0;

        // Fórmula completa: trading fees + withdrawal + slippage
        var buyCost = askPrice * volume * (1 + tradingFeeBuy);
        var sellGain = bidPrice * volume * (1 - tradingFeeSell);
        var slippage = askPrice * volume * _arbOptions.SlippagePct;
        var netProfit = sellGain - buyCost - slippage - withdrawalFee;
        var returnPct = buyCost > 0 ? netProfit / buyCost : 0;

        return new ArbitrageOpportunity(
            buySide.ExchangeId, sellSide.ExchangeId,
            askPrice, bidPrice, volume,
            netProfit, returnPct, DateTime.UtcNow);
    }

    /// <summary>
    /// Indica si la oportunidad supera el umbral mínimo para ejecutar.
    /// </summary>
    public bool IsExecutable(ArbitrageOpportunity opp)
    {
        return opp.ReturnPct > _arbOptions.MinReturnPct
               && opp.NetProfit > 0
               && opp.BidPrice > opp.AskPrice;
    }

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

    private ExchangeConfig? GetExchangeConfig(string exchangeId)
    {
        return _exchangeOptions.Exchanges.TryGetValue(exchangeId, out var cfg) ? cfg : null;
    }
}
