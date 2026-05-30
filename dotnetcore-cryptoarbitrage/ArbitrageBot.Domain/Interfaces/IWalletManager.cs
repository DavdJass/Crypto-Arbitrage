using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

/// <summary>
/// Gestiona los balances simulados en cada exchange.
/// Thread-safe.
/// </summary>
public interface IWalletManager
{
    WalletBalance GetBalance(string exchangeId);
    IReadOnlyDictionary<string, WalletBalance> GetAllBalances();
    bool TryExecuteBuy(string exchangeId, decimal usdtAmount, decimal btcAmount);
    bool TryExecuteSell(string exchangeId, decimal btcAmount, decimal usdtAmount);
}
