import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { FeedStatus } from '../types';

const statusColor: Record<string, string> = {
  connected: '#22c55e',
  connecting: '#f59e0b',
  disconnected: '#ef4444',
  fallback_rest: '#f59e0b',
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

  return (
    <div className="card">
      <h3>📡 Conexiones</h3>
      {feeds.map((f) => (
        <div key={f.exchangeId} className="card-row">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span
              style={{
                width: 10,
                height: 10,
                borderRadius: '50%',
                backgroundColor: statusColor[f.status] || '#666',
                display: 'inline-block',
              }}
            />
            <span>{f.exchangeId}</span>
          </div>
          <span className="dim">{f.status}</span>
        </div>
      ))}
    </div>
  );
}
