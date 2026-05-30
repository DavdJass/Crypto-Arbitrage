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
    private int _consecutiveLosses;
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
                    _consecutiveLosses = 0;
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
            _consecutiveLosses = losses;

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
            return new CircuitBreakerState(
                IsOpen: _openUntil is not null && DateTime.UtcNow < _openUntil,
                OpenedAt: _openUntil?.AddSeconds(-_options.CooldownSeconds),
                ClosedAt: _openUntil,
                ConsecutiveLosses: _consecutiveLosses,
                RecentTradesCount: _recentResults.Count
            );
        }
    }
}
