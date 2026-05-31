import { useEffect, useState } from 'react';
import type { TradeResult } from '../types';

export function TradeToast({ trades }: { trades: TradeResult[] }) {
  const [visible, setVisible] = useState<TradeResult | null>(null);

  useEffect(() => {
    if (trades.length === 0) return;
    const latest = trades[0];

    if (latest.isProfit && latest.status.startsWith('executed')) {
      setVisible(latest);
      const timer = setTimeout(() => setVisible(null), 5000);
      return () => clearTimeout(timer);
    }
  }, [trades]);

  if (!visible) return null;

  return (
    <div className="toast-profit">
      <div className="toast-profit-title">Trade ejecutado con ganancia</div>
      <div style={{ fontSize: 12, color: 'var(--text-dim)' }}>
        {new Date(visible.executedAt).toLocaleTimeString()}
      </div>
      <div className="exchange-pair" style={{ marginTop: 10, fontSize: 14 }}>
        <span className="gold">{visible.buyExchange}</span>
        <span className="exchange-pair-arrow">→</span>
        <span className="gold">{visible.sellExchange}</span>
      </div>
      <div className="toast-profit-amount">
        +${visible.netProfit.toFixed(3)}
        <span style={{ fontSize: 14, fontWeight: 500, color: 'var(--text-dim)', marginLeft: 10 }}>
          {(visible.returnPct * 100).toFixed(3)}%
        </span>
      </div>
    </div>
  );
}
