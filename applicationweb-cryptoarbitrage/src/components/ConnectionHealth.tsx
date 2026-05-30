import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { FeedStatus, TradeSummary } from '../types';

const statusIcon: Record<string, string> = { connected: '🟢', connecting: '🟡', disconnected: '🔴', fallback_rest: '🟠' };

export function ConnectionHealth() {
  const [feeds, setFeeds] = useState<FeedStatus[]>([]);
  const [summary, setSummary] = useState<TradeSummary | null>(null);

  useEffect(() => {
    const fetch = async () => {
      try {
        const [f, s] = await Promise.all([
          arbitrageApi.getConnections(),
          arbitrageApi.getTradeSummary().catch(() => null)
        ]);
        setFeeds(f);
        if (s) setSummary(s);
      } catch {}
    };
    fetch();
    const id = setInterval(fetch, 5000);
    return () => clearInterval(id);
  }, []);

  const allOk = feeds.every(f => f.status === 'connected');
  const avgLatency = feeds.length > 0
    ? feeds.reduce((a, f) => a + (f.avgLatencyMs || 0), 0) / feeds.length
    : 0;

  return (
    <div className="card" style={{ borderTop: `3px solid ${allOk ? 'var(--green)' : 'var(--red)'}` }}>
      <h3>
        <span className={`live-dot ${allOk ? 'connected' : 'disconnected'}`} />
        Rendimiento
      </h3>
      <div className="card-row">
        <span className="label">Latencia promedio</span>
        <span className="mono">{avgLatency.toFixed(1)} ms</span>
      </div>
      {summary && (
        <>
          <div className="card-row">
            <span className="label">Win Rate</span>
            <span className={`mono ${summary.winRate >= 0.5 ? 'green' : 'red'}`}>
              {(summary.winRate * 100).toFixed(1)}%
            </span>
          </div>
          <div className="card-row">
            <span className="label">Trades totales</span>
            <span className="mono">{summary.totalTrades}</span>
          </div>
          <div className="card-row">
            <span className="label">PnL Total</span>
            <span className={`mono ${summary.totalPnl >= 0 ? 'green' : 'red'}`}>
              ${summary.totalPnl.toFixed(3)}
            </span>
          </div>
        </>
      )}
      <div style={{ marginTop: 12, fontSize: 12, color: 'var(--text-dim)' }}>
        {feeds.map(f => (
          <div key={f.exchangeId} style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 0' }}>
            <span>{statusIcon[f.status] || '⚪'} {f.exchangeId}</span>
            {f.avgLatencyMs ? <span>{f.avgLatencyMs.toFixed(0)}ms</span> : <span>{f.status}</span>}
          </div>
        ))}
      </div>
    </div>
  );
}
