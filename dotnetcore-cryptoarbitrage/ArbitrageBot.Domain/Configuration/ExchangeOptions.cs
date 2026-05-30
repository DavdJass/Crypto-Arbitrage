namespace ArbitrageBot.Domain.Configuration;

/// <summary>
/// Configuración de fees y slippage por exchange.
/// </summary>
public class ExchangeOptions
{
    public const string SectionName = "Exchanges";

    public Dictionary<string, ExchangeConfig> Exchanges { get; set; } = new();
}

public class ExchangeConfig
{
    public decimal Fee { get; set; }          // ej: 0.001 = 0.1%
    public string WebSocketUrl { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
}
