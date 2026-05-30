namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Estado actual del circuit breaker.
/// </summary>
public record CircuitBreakerState(
    bool IsOpen,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    int ConsecutiveLosses,
    int RecentTradesCount
);
