import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { FeedStatus } from '../types';

const statusIcon: Record<string, string> = {
  connected: '🟢',
  connecting: '🟡',
  disconnected: '🔴',
  fallback_rest: '🟠',
};
const statusLabel: Record<string, string> = {
  connected: 'WebSocket',
  connecting: 'Conectando...',
  disconnected: 'Desconectado',
  fallback_rest: 'REST fallback',
};

export function ConnectionHealth() {
  const [feeds, setFeeds] = useState<FeedStatus[]>([]);

  useEffect(() => {
    const fetch = () =>
      arbitrageApi.getConnections().then(setFeeds).catch(() => {});
    fetch();
    const id = setInterval(fetch, 5000);
    return () => clearInterval(id);
  }, []);

  const allOk = feeds.every(f => f.status === 'connected');

  return (
    <div className="card" style={{ borderTop: `3px solid ${allOk ? 'var(--green)' : 'var(--red)'}` }}>
      <h3 style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span className={`live-dot ${allOk ? 'connected' : 'disconnected'}`} />
        Conexiones
      </h3>
      {feeds.map((f) => (
        <div key={f.exchangeId} className="card-row">
          <span>{f.exchangeId}</span>
          <span className="dim">{statusIcon[f.status] || '⚪'} {statusLabel[f.status] || f.status}</span>
        </div>
      ))}
    </div>
  );
}
