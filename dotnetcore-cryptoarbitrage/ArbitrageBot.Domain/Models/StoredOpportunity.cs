namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Oportunidad persistida con estado para historial y dashboard.
/// </summary>
public record StoredOpportunity(
    Guid Id,
    string BuyExchange,
    string SellExchange,
    decimal AskPrice,
    decimal BidPrice,
    decimal Volume,
    decimal NetProfit,
    decimal ReturnPct,
    string Status,
    string? Reason,
    DateTime DetectedAt
);
