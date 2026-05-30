using Microsoft.AspNetCore.Mvc;

namespace dotnetcore_cryptoarbitrage.Controllers;

// Controlador boilerplate de .NET — no usado. Mantenido como marcador.
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Este controlador no esta en uso. Visita /swagger para ver la API real.");
    }
}
