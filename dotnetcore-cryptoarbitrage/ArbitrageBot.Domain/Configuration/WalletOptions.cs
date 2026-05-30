namespace ArbitrageBot.Domain.Configuration;

/// <summary>
/// Configuración de wallets iniciales por exchange.
/// </summary>
public class WalletOptions
{
    public const string SectionName = "Wallets";

    public Dictionary<string, WalletConfig> Wallets { get; set; } = new();
}

public class WalletConfig
{
    public decimal InitialUsdt { get; set; }
    public decimal InitialBtc { get; set; }
}
