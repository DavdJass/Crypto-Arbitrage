using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ArbitrageBot.Domain.Configuration;

namespace ArbitrageBot.API.Middleware;

/// <summary>
/// Middleware de rate limiting por IP (sliding window).
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequestsPerMinute;
    private readonly ConcurrentDictionary<string, SlidingWindow> _clients = new();

    public RateLimitMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
    {
        _next = next;
        _maxRequestsPerMinute = options.Value.RateLimitPerMinute;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var window = _clients.GetOrAdd(clientIp, _ => new SlidingWindow(_maxRequestsPerMinute));

        if (!window.TryAcquire())
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers["Retry-After"] = "60";
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"Demasiadas solicitudes. Límite: " +
                _maxRequestsPerMinute + " por minuto\"}");
            return;
        }

        await _next(context);
    }

    private class SlidingWindow
    {
        private readonly int _maxRequests;
        private readonly ConcurrentQueue<DateTime> _timestamps = new();

        public SlidingWindow(int maxRequests)
        {
            _maxRequests = maxRequests;
        }

        public bool TryAcquire()
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-1);

            // Limpiar timestamps viejos
            while (_timestamps.TryPeek(out var ts) && ts < windowStart)
                _timestamps.TryDequeue(out _);

            if (_timestamps.Count >= _maxRequests)
                return false;

            _timestamps.Enqueue(now);
            return true;
        }
    }
}
