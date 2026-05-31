import { OrderBookPanel } from './OrderBookPanel';
import { OpportunitiesTable } from './OpportunitiesTable';
import { TradesPanel } from './TradesPanel';
import { WalletPanel } from './WalletPanel';
import { PnLChart } from './PnLChart';
import { CircuitBreakerPanel } from './CircuitBreakerPanel';
import { ConnectionHealth } from './ConnectionHealth';
import { TradeToast } from './TradeToast';
import { ThemeToggle } from './ThemeToggle';
import { HeroStats } from './HeroStats';
import { useSignalR } from '../hooks/useSignalR';

export function Dashboard() {
  const { connected, orderBooks, opportunities, trades, triangularOpps, clearOpportunities } =
    useSignalR();

  const executableCount = opportunities.filter(
    o => o.status === 'executed' || o.status === 'detected'
  ).length;
  const triangularCount = triangularOpps.length;

  return (
    <div className="dashboard">
      <header>
        <h1><span className="btc-icon">₿</span> Crypto Arbitrage Bot</h1>
        <div className="header-controls">
          <ThemeToggle />
          {executableCount > 0 && (
            <span className="badge badge-ok animate-pulse">
              {executableCount} ejecutables
            </span>
          )}
          {triangularCount > 0 && (
            <span className="badge badge-ok" style={{ background: 'rgba(240,185,11,.15)', color: 'var(--gold)' }}>
              🔺 {triangularCount} triangular
            </span>
          )}
          <span className={`badge ${connected ? 'badge-ok' : 'badge-err'}`}>
            <span className={`live-dot ${connected ? 'connected' : 'disconnected'}`} />
            {connected ? 'EN VIVO' : 'DESCONECTADO'}
          </span>
        </div>
      </header>

      <TradeToast trades={trades} />

      {/* Hero Stats */}
      <section>
        <HeroStats trades={trades} />
      </section>

      {/* Order Books + Status */}
      <section>
        <div className="section-header">
          <h2>📊 Order Books</h2>
        </div>
        <div className="top-row">
          <div className="col-2">
            <OrderBookPanel books={orderBooks} />
          </div>
          <div className="col-1">
            <ConnectionHealth />
            <CircuitBreakerPanel />
          </div>
        </div>
      </section>

      {/* Opportunities */}
      <section>
        <OpportunitiesTable ops={opportunities} onClear={clearOpportunities} />
      </section>

      {/* Triangular Arbitrage */}
      {triangularOpps.length > 0 && (
        <section>
          <div className="section-header">
            <h2>🔺 Arbitraje Triangular <span>({triangularOpps.length})</span></h2>
          </div>
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Exchange</th>
                  <th>Ruta</th>
                  <th>BTC Inicial</th>
                  <th>BTC Final</th>
                  <th>Profit BTC</th>
                  <th>Retorno</th>
                </tr>
              </thead>
              <tbody>
                {triangularOpps.map((op) => (
                  <tr key={`${op.exchangeId}-${op.detectedAt}`} className="row-executable" style={{ borderLeft: '3px solid var(--gold)' }}>
                    <td><span className="gold">{op.exchangeId}</span></td>
                    <td className="mono dim">{op.path}</td>
                    <td className="mono">{op.startAmountBtc.toFixed(6)}</td>
                    <td className="mono green">{op.endAmountBtc.toFixed(6)}</td>
                    <td className="mono green">+{op.netProfitBtc.toFixed(8)}</td>
                    <td className="mono green">{(op.returnPct * 100).toFixed(4)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* PnL + Wallets */}
      <section>
        <div className="mid-row">
          <div className="col-2">
            <PnLChart trades={trades} />
          </div>
          <div className="col-1">
            <WalletPanel />
          </div>
        </div>
      </section>

      {/* Trades */}
      <section>
        <TradesPanel trades={trades} />
      </section>

      <footer style={{
        textAlign: 'center', padding: '20px 0',
        color: 'var(--text-dim)', fontSize: 12,
        borderTop: '1px solid var(--border)',
      }}>
        Crypto Arbitrage Bot v1.0 — Datos en vivo de 10 exchanges: Binance, Kraken, Bybit, Coinbase, OKX, Bitfinex, KuCoin, Gate.io, Bitstamp, Gemini
      </footer>
    </div>
  );
}
