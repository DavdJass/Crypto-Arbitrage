using Microsoft.AspNetCore.Mvc;
using ArbitrageBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.API.Controllers;

/// <summary>
/// Snapshot actual de order books de todos los exchanges.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrderBooksController : ControllerBase
{
    private readonly IOrderBookAggregator _cache;
    private readonly ILogger<OrderBooksController> _logger;

    public OrderBooksController(IOrderBookAggregator cache, ILogger<OrderBooksController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el snapshot más reciente del order book de cada exchange conectado.
    /// </summary>
    /// <returns>
    /// Lista de order books con ExchangeId, BestBid, BestAsk, BidVolume, AskVolume,
    /// Spread (Ask−Bid), Timestamp y Age (antigüedad del dato).
    /// </returns>
    /// <response code="200">Order books retornados exitosamente.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        var books = _cache.GetAllOrderBooks();
        return Ok(books.Values.Select(b => new
        {
            b.ExchangeId,
            b.BestBid,
            b.BestAsk,
            b.BidVolume,
            b.AskVolume,
            Spread = b.BestAsk - b.BestBid,
            b.Timestamp,
            Age = DateTime.UtcNow - b.Timestamp
        }));
    }
}
