namespace ArbitrageBot.Domain.Configuration;

/// <summary>
/// Parámetros de detección y ejecución de arbitraje.
/// </summary>
public class ArbitrageOptions
{
    public const string SectionName = "Arbitrage";

    /// <summary>Volumen máximo por trade en BTC.</summary>
    public decimal MaxVolumeBtc { get; set; } = 0.1m;

    /// <summary>Umbral mínimo de retorno neto para ejecutar (0.2% = 0.002).</summary>
    public decimal MinReturnPct { get; set; } = 0.002m;

    /// <summary>Slippage estimado como fracción del volumen (0.05% = 0.0005).</summary>
    public decimal SlippagePct { get; set; } = 0.0005m;
}
