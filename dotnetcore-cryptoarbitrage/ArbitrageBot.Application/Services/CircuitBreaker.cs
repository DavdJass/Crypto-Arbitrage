using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Circuit Breaker: si hay N pérdidas consecutivas en trades ejecutados,
/// pausa la detección por CooldownSeconds segundos.
/// Thread-safe.
/// </summary>
public class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private int _consecutiveLosses;
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

    /// <summary>Registra el resultado de un trade ejecutado y decide si abrir el circuito.</summary>
    public void RecordTrade(bool isProfit)
    {
        lock (_lock)
        {
            if (isProfit)
            {
                _consecutiveLosses = 0;
                return;
            }

            _consecutiveLosses++;
            if (_consecutiveLosses >= _options.MaxLossesBeforeOpen)
                _openUntil = DateTime.UtcNow.AddSeconds(_options.CooldownSeconds);
        }
    }

    public CircuitBreakerState GetState()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var isExpired = _openUntil is not null && now >= _openUntil.Value;

            if (isExpired) _openUntil = null;

            var isOpen = _openUntil is not null;

            return new CircuitBreakerState(
                IsOpen: isOpen,
                OpenedAt: _openUntil?.AddSeconds(-_options.CooldownSeconds),
                OpenUntil: _openUntil,
                LossCountInWindow: _consecutiveLosses,
                RecentTradesCount: _consecutiveLosses,
                MaxLossesBeforeOpen: _options.MaxLossesBeforeOpen
            );
        }
    }
}
