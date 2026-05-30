using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

/// <summary>
/// Feed WebSocket de un exchange. Emite eventos de OrderBook.
/// </summary>
public interface IExchangeFeed
{
    string ExchangeId { get; }
    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync();
    event Action<OrderBook> OnOrderBookUpdated;
}
