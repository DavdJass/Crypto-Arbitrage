namespace ArbitrageBot.Domain.Configuration;

public class ArbitrageOptions
{
    public const string SectionName = "Arbitrage";
    public decimal MaxVolumeBtc { get; set; } = 0.1m;
    public decimal MinReturnPct { get; set; } = 0.002m;
    public decimal SlippagePct { get; set; } = 0.0005m;
    /// <summary>Latencia de red simulada en ms para realismo en la ejecución.</summary>
    public int NetworkLatencyMs { get; set; } = 200;
}
