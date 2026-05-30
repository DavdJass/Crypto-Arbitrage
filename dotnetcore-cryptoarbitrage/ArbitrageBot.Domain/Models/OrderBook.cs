namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Snapshot del order book de un exchange en un momento dado.
/// </summary>
public record OrderBook(
    string ExchangeId,
    decimal BestBid,
    decimal BestAsk,
    decimal BidVolume,
    decimal AskVolume,
    DateTime Timestamp
);
