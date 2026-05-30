using ArbitrageBot.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.API.Middleware;

/// <summary>
/// Middleware que valida API Key en todas las peticiones REST,
/// excepto Swagger y SignalR negotiation.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
    private readonly HashSet<string> _excludedPaths;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
    {
        _next = next;
        _apiKey = options.Value.ApiKey;
        _excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/swagger",
            "/swagger/v1/swagger.json",
            "/hubs/arbitrage",       // SignalR negotiation
            "/hubs/arbitrage/negotiate"
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Saltar rutas excluidas
        var path = context.Request.Path.Value ?? "";
        foreach (var excluded in _excludedPaths)
        {
            if (path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        // Validar API Key
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"API Key requerida. Agrega header X-API-Key\"}");
            return;
        }

        if (!string.Equals(extractedApiKey, _apiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"API Key inválida\"}");
            return;
        }

        await _next(context);
    }
}
