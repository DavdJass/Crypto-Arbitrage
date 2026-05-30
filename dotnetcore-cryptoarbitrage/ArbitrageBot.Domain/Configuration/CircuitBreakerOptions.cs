namespace ArbitrageBot.Domain.Configuration;

/// <summary>
/// Configuración del circuit breaker.
/// </summary>
public class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";

    /// <summary>Número de trades recientes a evaluar.</summary>
    public int WindowSize { get; set; } = 5;

    /// <summary>Si esta cantidad de los últimos WindowSize trades son pérdidas, se abre el circuito.</summary>
    public int MaxLossesBeforeOpen { get; set; } = 3;

    /// <summary>Segundos que el circuito permanece abierto antes de reanudar.</summary>
    public int CooldownSeconds { get; set; } = 30;
}
