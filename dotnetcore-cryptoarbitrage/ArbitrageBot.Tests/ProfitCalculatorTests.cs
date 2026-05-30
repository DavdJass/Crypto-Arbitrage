using ArbitrageBot.Application.Services;
using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArbitrageBot.Tests;

public class ProfitCalculatorTests
{
    private readonly ProfitCalculator _calc;

    public ProfitCalculatorTests()
    {
        var arbOpts = Options.Create(new ArbitrageOptions
        {
            MaxVolumeBtc = 0.1m,
            MinReturnPct = 0.002m,  // 0.2%
            SlippagePct = 0.0005m,  // 0.05%
            NetworkLatencyMs = 150
        });

        var exchangeOpts = Options.Create(new ExchangeOptions
        {
            Exchanges = new Dictionary<string, ExchangeConfig>
            {
                ["Binance"] = new() { Fee = 0.0010m, WithdrawalFeeUsdt = 2.00m },
                ["Kraken"] = new() { Fee = 0.0016m, WithdrawalFeeUsdt = 3.50m },
                ["Bybit"] = new() { Fee = 0.0010m, WithdrawalFeeUsdt = 1.50m },
            }
        });

        _calc = new ProfitCalculator(arbOpts, exchangeOpts);
    }

    [Fact]
    public void Evaluate_ProfitableSpread_ReturnsPositiveProfit()
    {
        var binance = new OrderBook("Binance", 70000m, 70050m, 2.0m, 1.5m, DateTime.UtcNow);
        var kraken = new OrderBook("Kraken", 70400m, 70450m, 1.8m, 1.2m, DateTime.UtcNow);

        var opp = _calc.Evaluate(binance, kraken);

        // Buy Binance @ 70050, Sell Kraken @ 70400
        // buyCost = 70050*0.1*1.001 = 7012.005
        // sellGain = 70400*0.1*0.9984 = 7028.736
        // slippage = 70050*0.1*0.0005 = 3.5025
        // withdrawal (Binance) = 2.00
        // netProfit = 7028.736 - 7012.005 - 3.5025 - 2.00 = 11.2285
        Assert.True(opp.NetProfit > 0, $"Esperado profit positivo, fue {opp.NetProfit}");
        Assert.True(opp.ReturnPct > 0);
        Assert.True(opp.BidPrice > opp.AskPrice);
    }

    [Fact]
    public void Evaluate_NoSpread_ReturnsNegativeOrZero()
    {
        var binance = new OrderBook("Binance", 70000m, 70200m, 2.0m, 1.5m, DateTime.UtcNow);
        var kraken = new OrderBook("Kraken", 70000m, 70050m, 1.8m, 1.2m, DateTime.UtcNow);

        var opp = _calc.Evaluate(binance, kraken);

        // Kraken bid (70000) <= Binance ask (70200) → sin spread
        Assert.True(opp.NetProfit <= 0, $"Esperado profit <= 0, fue {opp.NetProfit}");
    }

    [Fact]
    public void IsExecutable_BelowThreshold_ReturnsFalse()
    {
        var opp = new ArbitrageOpportunity(
            "Binance", "Kraken", 70000m, 70014m, 0.1m,
            0.5m,         // ganancia neta muy pequeña
            0.00007m,     // 0.007% < 0.2% threshold
            DateTime.UtcNow);

        Assert.False(_calc.IsExecutable(opp));
    }

    [Fact]
    public void IsExecutable_AboveThreshold_ReturnsTrue()
    {
        var opp = new ArbitrageOpportunity(
            "Binance", "Kraken", 70000m, 70250m, 0.1m,
            15.0m,        // buena ganancia
            0.004m,       // 0.4% > 0.2% threshold
            DateTime.UtcNow);

        Assert.True(_calc.IsExecutable(opp));
    }

    [Fact]
    public void IsExecutable_NegativeProfit_ReturnsFalse()
    {
        var opp = new ArbitrageOpportunity(
            "Binance", "Kraken", 70000m, 70050m, 0.1m,
            -5.0m, -0.001m, DateTime.UtcNow);

        Assert.False(_calc.IsExecutable(opp));
    }

    [Fact]
    public void CalculateMaxVolume_RespectsCap()
    {
        var binance = new OrderBook("Binance", 70000m, 70050m, 5.0m, 3.0m, DateTime.UtcNow);
        var kraken = new OrderBook("Kraken", 70200m, 70250m, 4.0m, 2.0m, DateTime.UtcNow);

        var vol = _calc.CalculateMaxVolume(binance, kraken);

        // Liquidity = min(3.0, 4.0) = 3.0, pero cap = 0.1
        Assert.Equal(0.1m, vol);
    }

    [Fact]
    public void CalculateMaxVolume_RespectsLiquidity()
    {
        var binance = new OrderBook("Binance", 70000m, 70050m, 0.05m, 0.03m, DateTime.UtcNow);
        var kraken = new OrderBook("Kraken", 70200m, 70250m, 0.04m, 0.02m, DateTime.UtcNow);

        var vol = _calc.CalculateMaxVolume(binance, kraken);

        // Min(Cap=0.1, AskVol=0.03, BidVol=0.04) = 0.03
        Assert.Equal(0.03m, vol);
    }

    [Fact]
    public void Evaluate_AccountsForWithdrawalFee()
    {
        // Spread muy ajustado — el withdrawal fee hace que sea negativo
        var binance = new OrderBook("Binance", 70000m, 70050m, 2.0m, 1.5m, DateTime.UtcNow);
        var kraken = new OrderBook("Kraken", 70100m, 70150m, 1.8m, 1.2m, DateTime.UtcNow);

        var opp = _calc.Evaluate(binance, kraken);

        // Spread de solo $50 — tras fees + withdrawal debe ser negativo
        Assert.True(opp.NetProfit < 0, $"Spread ajustado debe ser negativo, fue {opp.NetProfit}");
    }
}
