import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { WalletBalance } from '../types';

export function WalletPanel() {
  const [wallets, setWallets] = useState<WalletBalance[]>([]);

  useEffect(() => {
    const fetch = () =>
      arbitrageApi.getWallets().then(setWallets).catch(() => {});
    fetch();
    const id = setInterval(fetch, 5000);
    return () => clearInterval(id);
  }, []);

  const totalUsdt = wallets.reduce((s, w) => s + w.usdtBalance, 0);
  const totalBtc = wallets.reduce((s, w) => s + w.btcBalance, 0);

  return (
    <div>
      <div className="section-header">
        <h2>💰 Wallets</h2>
        <span className="dim">
          Total: ${totalUsdt.toLocaleString()} USDT | {totalBtc.toFixed(4)} BTC
        </span>
      </div>
      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Exchange</th>
              <th>USDT</th>
              <th>BTC</th>
            </tr>
          </thead>
          <tbody>
            {wallets.map((w) => (
              <tr key={w.exchangeId}>
                <td>{w.exchangeId}</td>
                <td className="mono">${w.usdtBalance.toLocaleString()}</td>
                <td className="mono">{w.btcBalance.toFixed(6)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
