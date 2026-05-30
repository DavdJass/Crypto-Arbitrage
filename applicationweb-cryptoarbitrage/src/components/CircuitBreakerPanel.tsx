import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { CircuitBreakerState } from '../types';

export function CircuitBreakerPanel() {
  const [cb, setCb] = useState<CircuitBreakerState | null>(null);

  useEffect(() => {
    const fetch = () =>
      arbitrageApi.getCircuitBreaker().then(setCb).catch(() => {});
    fetch();
    const id = setInterval(fetch, 5000);
    return () => clearInterval(id);
  }, []);

  if (!cb) return null;

  return (
    <div className="card" style={{ borderTop: `3px solid ${cb.isOpen ? '#ef4444' : '#22c55e'}` }}>
      <h3>🛡️ Circuit Breaker</h3>
      <div className="card-row">
        <span className="label">Estado</span>
        <span className={`badge ${cb.isOpen ? 'badge-err' : 'badge-ok'}`}>
          {cb.isOpen ? 'ABIERTO' : 'CERRADO'}
        </span>
      </div>
      <div className="card-row">
        <span className="label">Pérdidas consecutivas</span>
        <span className="mono">{cb.consecutiveLosses}/5</span>
      </div>
      <div className="card-row">
        <span className="label">Trades recientes</span>
        <span className="mono">{cb.recentTradesCount}</span>
      </div>
      {cb.openedAt && (
        <div className="card-row dim">
          <span>Abierto: {new Date(cb.openedAt).toLocaleTimeString()}</span>
        </div>
      )}
    </div>
  );
}
