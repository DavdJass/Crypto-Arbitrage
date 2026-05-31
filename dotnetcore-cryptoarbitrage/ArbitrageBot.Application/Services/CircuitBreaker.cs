using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Circuit Breaker: si N de los últimos M trades son pérdidas,
/// pausa la detección por CooldownSeconds segundos.
/// Thread-safe.
/// </summary>
public class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly LinkedList<bool> _recentResults = new(); // true = ganancia, false = pérdida
    private DateTime? _openUntil;
    private readonly object _lock = new();

    public CircuitBreaker(IOptions<CircuitBreakerOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>True si el circuito está abierto (no se deben ejecutar trades).</summary>
    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                if (_openUntil is null) return false;
                if (DateTime.UtcNow >= _openUntil.Value)
                {
                    _openUntil = null;
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>Registra el resultado de un trade y decide si abrir el circuito.</summary>
    public void RecordTrade(bool isProfit)
    {
        lock (_lock)
        {
            _recentResults.AddLast(isProfit);
            while (_recentResults.Count > _options.WindowSize)
                _recentResults.RemoveFirst();

            var losses = _recentResults.Count(r => !r);

            if (_recentResults.Count >= _options.WindowSize
                && losses >= _options.MaxLossesBeforeOpen)
            {
                _openUntil = DateTime.UtcNow.AddSeconds(_options.CooldownSeconds);
            }
        }
    }

    public CircuitBreakerState GetState()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var isExpired = _openUntil is not null && now >= _openUntil.Value;

            // Alinear estado expirado igual que el getter IsOpen
            if (isExpired) _openUntil = null;

            var isOpen = _openUntil is not null;
            var losses = _recentResults.Count(r => !r);

            return new CircuitBreakerState(
                IsOpen: isOpen,
                OpenedAt: _openUntil?.AddSeconds(-_options.CooldownSeconds),
                OpenUntil: _openUntil,
                LossCountInWindow: losses,
                RecentTradesCount: _recentResults.Count
            );
        }
    }
}
