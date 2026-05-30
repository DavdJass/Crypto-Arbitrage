using System.Collections.Concurrent;
using System.Threading.Channels;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Infrastructure.Cache;

/// <summary>
/// Caché en memoria de los últimos OrderBooks de cada exchange.
/// Thread-safe gracias a ConcurrentDictionary.
/// </summary>
public class MemoryOrderBookCache : IOrderBookAggregator
{
    private readonly ConcurrentDictionary<string, OrderBook> _orderBooks = new();
    private readonly Channel<OrderBook> _channel;

    public MemoryOrderBookCache()
    {
        _channel = Channel.CreateUnbounded<OrderBook>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public void UpdateOrderBook(OrderBook orderBook)
    {
        _orderBooks[orderBook.ExchangeId] = orderBook;
        _channel.Writer.TryWrite(orderBook);
    }

    public OrderBook? GetLatest(string exchangeId)
    {
        return _orderBooks.TryGetValue(exchangeId, out var ob) ? ob : null;
    }

    public IReadOnlyDictionary<string, OrderBook> GetAllOrderBooks()
    {
        return new Dictionary<string, OrderBook>(_orderBooks);
    }

    public ChannelReader<OrderBook> GetReader()
    {
        return _channel.Reader;
    }
}
