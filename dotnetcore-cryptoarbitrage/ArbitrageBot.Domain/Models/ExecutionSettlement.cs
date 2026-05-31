namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Desglose de costos para una ejecución simulada (fees, slippage, P&amp;L).
/// </summary>
public record ExecutionSettlement(
    decimal Volume,
    decimal BuyCostUsdt,
    decimal SellProceedsUsdt,
    decimal TradingFeesUsdt,
    decimal SlippageUsdt,
    decimal WithdrawalFeeUsdt,
    decimal LatencyCostUsdt,
    decimal NetProfit
);
