import { useEffect, useState } from 'react';
import type { TradeResult } from '../types';

interface Props {
  trades: TradeResult[];
}

/// Muestra un toast animado cuando se ejecuta un trade rentable.
/// Auto-desaparece tras 5 segundos.
export function TradeToast({ trades }: Props) {
  const [visible, setVisible] = useState<TradeResult | null>(null);

  useEffect(() => {
    if (trades.length === 0) return;
    const latest = trades[0];

    // Solo mostrar trades ejecutados con profit
    if (latest.isProfit && latest.status.startsWith('executed')) {
      setVisible(latest);
      const timer = setTimeout(() => setVisible(null), 5000);
      return () => clearTimeout(timer);
    }
  }, [trades]);

  if (!visible) return null;

  return (
    <div style={{
      position: 'fixed',
      top: 20,
      right: 20,
      zIndex: 9999,
      background: 'linear-gradient(135deg, #0a2e0a, #132d13)',
      border: '1px solid var(--green)',
      borderRadius: 12,
      padding: '16px 24px',
      boxShadow: '0 8px 32px rgba(0,200,83,.15)',
      animation: 'slideInRight .4s ease, fadeOut .5s ease 4.5s forwards',
      maxWidth: 380,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
        <span style={{ fontSize: 24 }}>💰</span>
        <div>
          <div style={{ fontWeight: 700, color: 'var(--green)', fontSize: 14 }}>
            ¡Trade Rentable Ejecutado!
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-dim)' }}>
            {new Date(visible.executedAt).toLocaleTimeString()}
          </div>
        </div>
      </div>
      <div style={{ fontSize: 13 }}>
        <span className="gold">{visible.buyExchange}</span>
        <span className="dim"> → </span>
        <span className="gold">{visible.sellExchange}</span>
      </div>
      <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--green)', marginTop: 6 }}>
        +${visible.netProfit.toFixed(3)}
        <span style={{ fontSize: 13, fontWeight: 400, color: 'var(--text-dim)', marginLeft: 8 }}>
          {(visible.returnPct * 100).toFixed(3)}%
        </span>
      </div>
    </div>
  );
}
