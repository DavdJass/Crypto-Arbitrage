using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

/// <summary>
/// Agrega order books de todos los exchanges y los publica en un Channel.
/// </summary>
public interface IOrderBookAggregator
{
    void UpdateOrderBook(OrderBook orderBook);
    OrderBook? GetLatest(string exchangeId);
    IReadOnlyDictionary<string, OrderBook> GetAllOrderBooks();
    System.Threading.Channels.ChannelReader<OrderBook> GetReader();
}
