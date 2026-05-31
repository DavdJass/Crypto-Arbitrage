using ArbitrageBot.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ArbitrageBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OpportunitiesController : ControllerBase
{
    private readonly IOpportunityRepository _repository;

    public OpportunitiesController(IOpportunityRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Historial de oportunidades detectadas (spread positivo o ejecutables).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 100)
    {
        var clamped = Math.Clamp(limit, 1, 500);
        var opportunities = await _repository.GetRecentAsync(clamped, HttpContext.RequestAborted);
        return Ok(opportunities);
    }
}
