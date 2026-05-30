namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Resultado de una ejecución de trade simulada.
/// </summary>
public record TradeResult(
    Guid Id,
    string BuyExchange,
    string SellExchange,
    decimal Volume,
    decimal NetProfit,
    decimal ReturnPct,
    bool IsProfit,
    string Status,     // "executed" | "insufficient_funds" | "circuit_open" | "below_threshold"
    DateTime ExecutedAt
);
