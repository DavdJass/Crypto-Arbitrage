import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { FeedStatus, TradeSummary } from '../types';

const statusIcon: Record<string, string> = {
  connected:      '🟢',
  connecting:     '🟡',
  disconnected:   '🔴',
  fallback_rest:  '🟠',
};

export function ConnectionHealth() {
  const [feeds, setFeeds] = useState<FeedStatus[] | null>(null);
  const [summary, setSummary] = useState<TradeSummary | null>(null);

  useEffect(() => {
    const doFetch = async () => {
      try {
        const [f, s] = await Promise.all([
          arbitrageApi.getConnections(),
          arbitrageApi.getTradeSummary().catch(() => null),
        ]);
        setFeeds(f);
        if (s) setSummary(s);
      } catch {}
    };
    doFetch();
    const id = setInterval(doFetch, 5000);
    return () => clearInterval(id);
  }, []);

  // Mientras carga, mostrar skeleton
  if (feeds === null) {
    return (
      <div className="card" style={{ borderTop: '3px solid var(--border)' }}>
        <h3><span className="live-dot connecting" />Rendimiento</h3>
        <div className="dim" style={{ fontSize: 13, marginTop: 8 }}>Cargando estado de feeds…</div>
      </div>
    );
  }

  // feeds.length === 0 → ⚠️, not allOk (vacío no significa "todos bien")
  const allOk = feeds.length > 0 && feeds.every(f => f.status === 'connected');
  const hasIssues = feeds.some(f => f.status !== 'connected');

  const avgLatency = feeds.length > 0
    ? feeds.reduce((a, f) => a + (f.avgLatencyMs ?? 0), 0) / feeds.length
    : 0;

  const headerColor = allOk ? 'var(--green)' : hasIssues ? 'var(--red)' : 'var(--yellow, #f59e0b)';

  return (
    <div className="card" style={{ borderTop: `3px solid ${headerColor}` }}>
      <h3>
        <span className={`live-dot ${allOk ? 'connected' : 'disconnected'}`} />
        Rendimiento
      </h3>

      <div className="card-row">
        <span className="label">Feeds activos</span>
        <span className={`mono ${allOk ? 'green' : hasIssues ? 'red' : ''}`}>
          {feeds.filter(f => f.status === 'connected').length}/{feeds.length}
        </span>
      </div>

      {feeds.length > 0 && (
        <div className="card-row">
          <span className="label">Latencia promedio</span>
          <span className="mono">{avgLatency.toFixed(1)} ms</span>
        </div>
      )}

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
              {summary.totalPnl >= 0 ? '+' : ''}${summary.totalPnl.toFixed(3)}
            </span>
          </div>
        </>
      )}

      <div style={{ marginTop: 12, fontSize: 12, color: 'var(--text-dim)' }}>
        {feeds.map(f => (
          <div key={f.exchangeId} style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 0' }}>
            <span>{statusIcon[f.status] ?? '⚪'} {f.exchangeId}</span>
            {f.avgLatencyMs != null
              ? <span>{f.avgLatencyMs.toFixed(0)} ms</span>
              : <span className="dim">{f.status}</span>}
          </div>
        ))}
        {feeds.length === 0 && (
          <div className="dim">Sin feeds registrados aún…</div>
        )}
      </div>
    </div>
  );
}
