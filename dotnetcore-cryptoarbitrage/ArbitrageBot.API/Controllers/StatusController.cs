using Microsoft.AspNetCore.Mvc;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Application.Services;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.API.Controllers;

/// <summary>
/// Estado del sistema: balances de wallets y circuit breaker.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusController : ControllerBase
{
    private readonly IWalletManager _walletManager;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        IWalletManager walletManager,
        CircuitBreaker circuitBreaker,
        ILogger<StatusController> logger)
    {
        _walletManager = walletManager;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el balance actual de todas las wallets simuladas por exchange.
    /// </summary>
    /// <returns>Lista de wallets con ExchangeId, UsdtBalance y BtcBalance.</returns>
    /// <response code="200">Balances retornados exitosamente.</response>
    [HttpGet("wallets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetWallets()
    {
        var balances = _walletManager.GetAllBalances();
        return Ok(balances.Values.Select(b => new
        {
            b.ExchangeId,
            b.UsdtBalance,
            b.BtcBalance
        }));
    }

    /// <summary>
    /// Obtiene el estado actual del circuit breaker.
    /// </summary>
    /// <returns>Objeto con IsOpen, OpenedAt, ClosedAt, ConsecutiveLosses y RecentTradesCount.</returns>
    /// <response code="200">Estado del circuit breaker retornado exitosamente.</response>
    [HttpGet("circuit-breaker")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCircuitBreaker()
    {
        var state = _circuitBreaker.GetState();
        return Ok(new
        {
            state.IsOpen,
            state.OpenedAt,
            state.ClosedAt,
            state.ConsecutiveLosses,
            state.RecentTradesCount
        });
    }
}
