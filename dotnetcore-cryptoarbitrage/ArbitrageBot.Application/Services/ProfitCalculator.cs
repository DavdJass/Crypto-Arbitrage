using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Calcula la rentabilidad neta de una oportunidad de arbitraje entre dos order books.
/// Siempre retorna el objeto <see cref="ArbitrageOpportunity"/> (incluso si no es rentable),
/// para que el frontend pueda visualizar todas las divergencias detectadas.
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
    /// Siempre retorna un <see cref="ArbitrageOpportunity"/> con los datos de la divergencia.
    /// </summary>
    public ArbitrageOpportunity Evaluate(OrderBook buySide, OrderBook sellSide)
    {
        var askPrice = buySide.BestAsk;
        var bidPrice = sellSide.BestBid;
        var volume = _arbOptions.MaxVolumeBtc;

        // Si no hay precios válidos, retornar oportunidad con profit cero
        if (askPrice <= 0 || bidPrice <= 0)
        {
            return new ArbitrageOpportunity(
                BuyExchange: buySide.ExchangeId,
                SellExchange: sellSide.ExchangeId,
                AskPrice: askPrice,
                BidPrice: bidPrice,
                Volume: volume,
                NetProfit: 0,
                ReturnPct: 0,
                DetectedAt: DateTime.UtcNow
            );
        }

        var buyExchangeConfig = GetExchangeConfig(buySide.ExchangeId);
        var sellExchangeConfig = GetExchangeConfig(sellSide.ExchangeId);
        var feeBuyer = buyExchangeConfig?.Fee ?? 0.001m;
        var feeSeller = sellExchangeConfig?.Fee ?? 0.001m;

        // Fórmula de rentabilidad
        var buyCost = askPrice * volume * (1 + feeBuyer);
        var sellGain = bidPrice * volume * (1 - feeSeller);
        var slippage = askPrice * volume * _arbOptions.SlippagePct;
        var netProfit = sellGain - buyCost - slippage;
        var returnPct = buyCost > 0 ? netProfit / buyCost : 0;

        return new ArbitrageOpportunity(
            BuyExchange: buySide.ExchangeId,
            SellExchange: sellSide.ExchangeId,
            AskPrice: askPrice,
            BidPrice: bidPrice,
            Volume: volume,
            NetProfit: netProfit,
            ReturnPct: returnPct,
            DetectedAt: DateTime.UtcNow
        );
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

    private ExchangeConfig? GetExchangeConfig(string exchangeId)
    {
        return _exchangeOptions.Exchanges.TryGetValue(exchangeId, out var cfg) ? cfg : null;
    }
}
