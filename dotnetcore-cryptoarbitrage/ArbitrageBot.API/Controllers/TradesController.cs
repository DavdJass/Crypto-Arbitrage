using Microsoft.AspNetCore.Mvc;
using ArbitrageBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.API.Controllers;

/// <summary>
/// Historial de trades ejecutados y resumen de rendimiento (PnL).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TradesController : ControllerBase
{
    private readonly ITradeRepository _tradeRepo;
    private readonly ILogger<TradesController> _logger;

    public TradesController(ITradeRepository tradeRepo, ILogger<TradesController> logger)
    {
        _tradeRepo = tradeRepo;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene los últimos N trades ejecutados, ordenados por fecha descendente.
    /// </summary>
    /// <param name="limit">Cantidad máxima de trades a retornar (default 50).</param>
    /// <returns>Lista de <see cref="ArbitrageBot.Domain.Models.TradeResult"/>.</returns>
    /// <response code="200">Trades retornados exitosamente.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 50)
    {
        var trades = await _tradeRepo.GetRecentAsync(limit, HttpContext.RequestAborted);
        return Ok(trades);
    }

    /// <summary>
    /// Obtiene el resumen de rendimiento acumulado: PnL total, cantidad de trades y win rate.
    /// </summary>
    /// <returns>Objeto con TotalPnl, TotalTrades, WinningTrades y WinRate.</returns>
    /// <response code="200">Resumen retornado exitosamente.</response>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var (pnl, total, wins) = await _tradeRepo.GetSummaryAsync(HttpContext.RequestAborted);
        return Ok(new
        {
            TotalPnl = pnl,
            TotalTrades = total,
            WinningTrades = wins,
            WinRate = total > 0 ? (double)wins / total : 0
        });
    }
}
