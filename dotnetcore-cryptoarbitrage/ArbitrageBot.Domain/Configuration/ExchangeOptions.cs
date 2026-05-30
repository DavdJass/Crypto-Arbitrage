namespace ArbitrageBot.Domain.Configuration;

/// <summary>
/// Configuración de fees, endpoints y símbolos por exchange.
/// </summary>
public class ExchangeOptions
{
    public const string SectionName = "Exchanges";

    public Dictionary<string, ExchangeConfig> Exchanges { get; set; } = new();
}

public class ExchangeConfig
{
    /// <summary>Trading fee como fracción (ej: 0.001 = 0.1%).</summary>
    public decimal Fee { get; set; }

    /// <summary>Withdrawal/retiro fee estimado en USDT.</summary>
    public decimal WithdrawalFeeUsdt { get; set; }

    /// <summary>Símbolo del par (ej: "btcusdt", "XBT/USD").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>URL del WebSocket público.</summary>
    public string WebSocketUrl { get; set; } = string.Empty;

    /// <summary>URL de la API REST para order book (fallback).</summary>
    public string RestUrl { get; set; } = string.Empty;
}
