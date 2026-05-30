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

  return (
    <div className="dashboard">
      {/* Header */}
      <header>
        <h1>
          <span className="btc-icon">₿</span> Crypto Arbitrage Bot
        </h1>
        <span className={`badge ${connected ? 'badge-ok' : 'badge-err'}`}>
          {connected ? '🟢 Live' : '🔴 Desconectado'}
        </span>
      </header>

      {/* Top row: OrderBooks + Status */}
      <section className="top-row">
        <div className="col-2">
          <h2>📊 Order Books en vivo</h2>
          <OrderBookPanel books={orderBooks} />
        </div>
        <div className="col-1">
          <ConnectionHealth />
          <CircuitBreakerPanel />
        </div>
      </section>

      {/* Opportunities */}
      <section>
        <OpportunitiesTable ops={opportunities} onClear={clearOpportunities} />
      </section>

      {/* PnL + Wallets */}
      <section className="mid-row">
        <div className="col-2">
          <PnLChart />
        </div>
        <div className="col-1">
          <WalletPanel />
        </div>
      </section>

      {/* Trades */}
      <section>
        <TradesPanel trades={trades} />
      </section>
    </div>
  );
}
