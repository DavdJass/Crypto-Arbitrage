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

  const maxLosses = cb.maxLossesBeforeOpen ?? 3;
  const pct = Math.min(100, (cb.lossCountInWindow / maxLosses) * 100);
  const barColor = cb.isOpen
    ? 'var(--red)'
    : pct >= 60
    ? 'var(--yellow, #f59e0b)'
    : 'var(--green)';

  return (
    <div className="card" style={{
      borderTop: `3px solid ${cb.isOpen ? 'var(--red)' : 'var(--green)'}`
    }}>
      <h3>🛡️ Circuit Breaker</h3>

      <div className="card-row">
        <span className="label">Estado</span>
        <span className={`badge ${cb.isOpen ? 'badge-err' : 'badge-ok'}`}>
          {cb.isOpen ? '⚡ ABIERTO' : '✅ CERRADO'}
        </span>
      </div>

      <div className="card-row">
        <span className="label">Pérdidas consecutivas</span>
        <span className="mono">
          {cb.lossCountInWindow}<span className="dim">/{maxLosses}</span>
        </span>
      </div>

      {/* Barra de progreso de riesgo */}
      <div style={{ margin: '4px 0 8px' }}>
        <div style={{
          height: 6,
          borderRadius: 3,
          background: 'rgba(255,255,255,0.08)',
          overflow: 'hidden'
        }}>
          <div style={{
            width: `${pct}%`,
            height: '100%',
            background: barColor,
            transition: 'width 0.4s ease, background 0.4s ease'
          }} />
        </div>
      </div>

      <div className="card-row">
        <span className="label">Trades evaluados</span>
        <span className="mono">{cb.recentTradesCount}</span>
      </div>

      {cb.openedAt && (
        <div className="card-row">
          <span className="label">Abierto desde</span>
          <span className="dim">{new Date(cb.openedAt).toLocaleTimeString()}</span>
        </div>
      )}

      {cb.openUntil && (
        <div className="card-row">
          <span className="label">Reanuda a las</span>
          <span className="mono" style={{ color: 'var(--yellow, #f59e0b)' }}>
            {new Date(cb.openUntil).toLocaleTimeString()}
          </span>
        </div>
      )}
    </div>
  );
}
