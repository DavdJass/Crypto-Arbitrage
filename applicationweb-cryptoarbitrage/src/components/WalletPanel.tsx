import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { WalletBalance } from '../types';

const INITIAL_USDT_PER_EXCHANGE = 50_000;
const INITIAL_BTC_PER_EXCHANGE  = 0.5;

function Delta({ value, decimals = 2, prefix = '$' }: { value: number; decimals?: number; prefix?: string }) {
  const sign = value >= 0 ? '+' : '';
  const color = value > 0 ? 'var(--green)' : value < 0 ? 'var(--red)' : 'var(--text-dim)';
  return (
    <span style={{ color, fontSize: 11, marginLeft: 4 }}>
      ({sign}{prefix}{Math.abs(value).toFixed(decimals)})
    </span>
  );
}

export function WalletPanel() {
  const [wallets, setWallets] = useState<WalletBalance[]>([]);

  useEffect(() => {
    const doFetch = () =>
      arbitrageApi.getWallets().then(setWallets).catch(() => {});
    doFetch();
    const id = setInterval(doFetch, 10_000);
    return () => clearInterval(id);
  }, []);

  const n = wallets.length || 1;
  const initialUsdtTotal = INITIAL_USDT_PER_EXCHANGE * n;
  const initialBtcTotal  = INITIAL_BTC_PER_EXCHANGE  * n;

  const totals = wallets.reduce(
    (a, w) => ({ usdt: a.usdt + w.usdtBalance, btc: a.btc + w.btcBalance }),
    { usdt: 0, btc: 0 }
  );

  const deltaUsdtTotal = totals.usdt - initialUsdtTotal;
  const deltaBtcTotal  = totals.btc  - initialBtcTotal;

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
              <th>ΔUSDT</th>
              <th>BTC</th>
              <th>ΔBTC</th>
            </tr>
          </thead>
          <tbody>
            {wallets.map((w) => {
              const dUsdt = w.usdtBalance - INITIAL_USDT_PER_EXCHANGE;
              const dBtc  = w.btcBalance  - INITIAL_BTC_PER_EXCHANGE;
              return (
                <tr key={w.exchangeId}>
                  <td><span className="gold">{w.exchangeId}</span></td>
                  <td className="mono green">
                    ${w.usdtBalance.toLocaleString(undefined, { maximumFractionDigits: 2 })}
                  </td>
                  <td>
                    <span
                      className="mono"
                      style={{ color: dUsdt >= 0 ? 'var(--green)' : 'var(--red)', fontSize: 12 }}
                    >
                      {dUsdt >= 0 ? '+' : ''}{dUsdt.toFixed(2)}
                    </span>
                  </td>
                  <td className="mono">{w.btcBalance.toFixed(6)}</td>
                  <td>
                    <span
                      className="mono"
                      style={{ color: dBtc >= 0 ? 'var(--green)' : 'var(--red)', fontSize: 12 }}
                    >
                      {dBtc >= 0 ? '+' : ''}{dBtc.toFixed(6)}
                    </span>
                  </td>
                </tr>
              );
            })}

            {wallets.length === 0 && (
              <tr>
                <td colSpan={5} style={{ textAlign: 'center', padding: '16px', color: 'var(--text-dim)' }}>
                  Cargando balances…
                </td>
              </tr>
            )}

            <tr style={{ borderTop: '2px solid var(--border)' }}>
              <td><strong>Total</strong></td>
              <td className="mono gold">
                ${totals.usdt.toLocaleString(undefined, { maximumFractionDigits: 2 })}
              </td>
              <td>
                <span
                  className="mono"
                  style={{ color: deltaUsdtTotal >= 0 ? 'var(--green)' : 'var(--red)', fontWeight: 600 }}
                >
                  {deltaUsdtTotal >= 0 ? '+' : ''}{deltaUsdtTotal.toFixed(2)}
                </span>
              </td>
              <td className="mono gold">{totals.btc.toFixed(6)}</td>
              <td>
                <span
                  className="mono"
                  style={{ color: deltaBtcTotal >= 0 ? 'var(--green)' : 'var(--red)', fontWeight: 600 }}
                >
                  {deltaBtcTotal >= 0 ? '+' : ''}{deltaBtcTotal.toFixed(6)}
                </span>
              </td>
            </tr>
          </tbody>
        </table>

        <div style={{ padding: '8px 4px', fontSize: 11, color: 'var(--text-dim)', marginTop: 4 }}>
          ℹ️ Δ relativo a balance inicial de{' '}
          <span className="mono">${INITIAL_USDT_PER_EXCHANGE.toLocaleString()} USDT</span> /{' '}
          <span className="mono">{INITIAL_BTC_PER_EXCHANGE} BTC</span> por exchange
        </div>
      </div>
    </div>
  );
}
