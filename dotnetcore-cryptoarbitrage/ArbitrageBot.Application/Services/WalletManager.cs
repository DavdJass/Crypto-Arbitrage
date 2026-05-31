using System.Collections.Concurrent;
using ArbitrageBot.Domain.Configuration;
using ArbitrageBot.Domain.Interfaces;
using ArbitrageBot.Domain.Models;
using Microsoft.Extensions.Options;

namespace ArbitrageBot.Application.Services;

/// <summary>
/// Gestiona los balances simulados por exchange. Thread-safe.
/// </summary>
public class WalletManager : IWalletManager
{
    private readonly ConcurrentDictionary<string, WalletBalance> _wallets = new();
    private readonly object _lock = new();

    public WalletManager(IOptions<WalletOptions> options)
    {
        foreach (var kvp in options.Value.Wallets)
        {
            _wallets[kvp.Key] = new WalletBalance(
                kvp.Key,
                kvp.Value.InitialUsdt,
                kvp.Value.InitialBtc
            );
        }
    }

    public WalletBalance GetBalance(string exchangeId)
    {
        return _wallets.GetValueOrDefault(exchangeId)
            ?? new WalletBalance(exchangeId, 0, 0);
    }

    public IReadOnlyDictionary<string, WalletBalance> GetAllBalances()
    {
        return new Dictionary<string, WalletBalance>(_wallets);
    }

    public bool TryExecuteBuy(string exchangeId, decimal usdtAmount, decimal btcAmount)
    {
        lock (_lock)
        {
            if (!_wallets.TryGetValue(exchangeId, out var balance))
                return false;

            if (balance.UsdtBalance < usdtAmount)
                return false;

            _wallets[exchangeId] = balance with
            {
                UsdtBalance = balance.UsdtBalance - usdtAmount,
                BtcBalance = balance.BtcBalance + btcAmount
            };
            return true;
        }
    }

    public bool TryExecuteSell(string exchangeId, decimal btcAmount, decimal usdtAmount)
    {
        lock (_lock)
        {
            if (!_wallets.TryGetValue(exchangeId, out var balance))
                return false;

            if (balance.BtcBalance < btcAmount)
                return false;

            _wallets[exchangeId] = balance with
            {
                BtcBalance = balance.BtcBalance - btcAmount,
                UsdtBalance = balance.UsdtBalance + usdtAmount
            };
            return true;
        }
    }

    /// <summary>
    /// Ejecuta compra y venta usando el desglose de costos del ProfitCalculator.
    /// </summary>
    public bool TryExecuteArbitrage(
        string buyExchange,
        string sellExchange,
        ExecutionSettlement settlement)
    {
        if (settlement.Volume <= 0)
            return false;

        lock (_lock)
        {
            if (!_wallets.TryGetValue(buyExchange, out var buyWallet) ||
                !_wallets.TryGetValue(sellExchange, out var sellWallet))
                return false;

            var totalBuyDebit = settlement.BuyCostUsdt
                + settlement.WithdrawalFeeUsdt
                + settlement.LatencyCostUsdt;

            if (buyWallet.UsdtBalance < totalBuyDebit ||
                sellWallet.BtcBalance < settlement.Volume)
                return false;

            _wallets[buyExchange] = buyWallet with
            {
                UsdtBalance = buyWallet.UsdtBalance - totalBuyDebit,
                BtcBalance = buyWallet.BtcBalance + settlement.Volume
            };

            _wallets[sellExchange] = sellWallet with
            {
                BtcBalance = sellWallet.BtcBalance - settlement.Volume,
                UsdtBalance = sellWallet.UsdtBalance + settlement.SellProceedsUsdt
            };

            return true;
        }
    }
}
