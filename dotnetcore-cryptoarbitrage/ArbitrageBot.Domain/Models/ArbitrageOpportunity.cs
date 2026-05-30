namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Oportunidad de arbitraje detectada entre dos exchanges.
/// </summary>
public record ArbitrageOpportunity(
    string BuyExchange,
    string SellExchange,
    decimal AskPrice,
    decimal BidPrice,
    decimal Volume,
    decimal NetProfit,
    decimal ReturnPct,
    DateTime DetectedAt
);
