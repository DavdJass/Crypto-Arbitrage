import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { WalletBalance } from '../types';

export function WalletPanel() {
  const [wallets, setWallets] = useState<WalletBalance[]>([]);

  useEffect(() => {
    const fetch = () =>
      arbitrageApi.getWallets().then(setWallets).catch(() => {});
    fetch();
    const id = setInterval(fetch, 10000);
    return () => clearInterval(id);
  }, []);

  const totals = wallets.reduce(
    (a, w) => ({ usdt: a.usdt + w.usdtBalance, btc: a.btc + w.btcBalance }),
    { usdt: 0, btc: 0 }
  );

  return (
    <div>
      <div className="section-header">
        <h2>💰 Wallets</h2>
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
                <td><span className="gold">{w.exchangeId}</span></td>
                <td className="mono green">${w.usdtBalance.toLocaleString()}</td>
                <td className="mono">{w.btcBalance.toFixed(6)}</td>
              </tr>
            ))}
            <tr style={{ borderTop: '2px solid var(--border)' }}>
              <td><strong>Total</strong></td>
              <td className="mono gold">${totals.usdt.toLocaleString()}</td>
              <td className="mono gold">{totals.btc.toFixed(6)}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  );
}
