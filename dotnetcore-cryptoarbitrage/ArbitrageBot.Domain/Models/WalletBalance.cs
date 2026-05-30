namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Estado de una wallet en un exchange.
/// </summary>
public record WalletBalance(
    string ExchangeId,
    decimal UsdtBalance,
    decimal BtcBalance
);
