using Microsoft.AspNetCore.Mvc;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Application.Services;
using ArbitrageBot.Infrastructure.Feeds;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusController : ControllerBase
{
    private readonly IWalletManager _walletManager;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly FeedHealthTracker _health;
    private readonly ILogger<StatusController> _logger;

    public StatusController(IWalletManager wm, CircuitBreaker cb, FeedHealthTracker h, ILogger<StatusController> l)
    { _walletManager = wm; _circuitBreaker = cb; _health = h; _logger = l; }

    [HttpGet("wallets")]
    public IActionResult GetWallets()
    {
        return Ok(_walletManager.GetAllBalances().Values.Select(b => new { b.ExchangeId, b.UsdtBalance, b.BtcBalance }));
    }

    [HttpGet("circuit-breaker")]
    public IActionResult GetCircuitBreaker()
    {
        var s = _circuitBreaker.GetState();
        return Ok(new { s.IsOpen, s.OpenedAt, s.ClosedAt, s.ConsecutiveLosses, s.RecentTradesCount });
    }

    [HttpGet("connections")]
    public IActionResult GetConnections()
    {
        return Ok(_health.GetAllStatuses().Values.Select(s => new
        {
            s.ExchangeId, s.Status, s.Details, s.LastUpdated,
            Age = DateTime.UtcNow - s.LastUpdated,
            s.AvgLatencyMs
        }));
    }
}
