import { OrderBookPanel } from './OrderBookPanel';
import { OpportunitiesTable } from './OpportunitiesTable';
import { TradesPanel } from './TradesPanel';
import { WalletPanel } from './WalletPanel';
import { PnLChart } from './PnLChart';
import { CircuitBreakerPanel } from './CircuitBreakerPanel';
import { ConnectionHealth } from './ConnectionHealth';
import { useSignalR } from '../hooks/useSignalR';

export function Dashboard() {
  const { connected, orderBooks, opportunities, trades, clearOpportunities } =
    useSignalR();

  const executableCount = opportunities.filter(
    (o) => o.netProfit > 0 && o.returnPct > 0.002 && o.bidPrice > o.askPrice
  ).length;

  return (
    <div className="dashboard">
      <header>
        <h1><span className="btc-icon">₿</span> Crypto Arbitrage Bot</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          {executableCount > 0 && (
            <span className="badge badge-ok">{executableCount} ejecutables</span>
          )}
          <span className={`badge ${connected ? 'badge-ok' : 'badge-err'}`}>
            <span className={`live-dot ${connected ? 'connected' : 'disconnected'}`} />
            {connected ? 'EN VIVO' : 'DESCONECTADO'}
          </span>
        </div>
      </header>

      {/* Top row: Order Books + Status */}
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

      {/* PnL + Wallets */}
      <section>
        <div className="mid-row">
          <div className="col-2">
            <PnLChart />
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

      <footer style={{ textAlign: 'center', padding: '20px 0', color: 'var(--text-dim)', fontSize: 12, borderTop: '1px solid var(--border)' }}>
        Crypto Arbitrage Bot v1.0 — Datos en vivo de Binance, Kraken y Bybit
      </footer>
    </div>
  );
}
