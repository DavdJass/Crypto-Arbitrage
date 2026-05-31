namespace ArbitrageBot.Domain.Configuration;

public class ArbitrageOptions
{
    public const string SectionName = "Arbitrage";
    public decimal MaxVolumeBtc { get; set; } = 0.1m;
    public decimal MinReturnPct { get; set; } = 0.002m;
    public decimal SlippagePct { get; set; } = 0.0005m;
    /// <summary>Latencia de red simulada en ms para realismo en la ejecución.</summary>
    public int NetworkLatencyMs { get; set; } = 200;
    /// <summary>Costo de oportunidad por segundo de latencia (ej. 0.001 = 0.1%/s sobre notional de compra).</summary>
    public decimal LatencyRiskPctPerSecond { get; set; } = 0.001m;
}
