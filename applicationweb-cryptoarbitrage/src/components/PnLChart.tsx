import { useEffect, useState } from 'react';
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import { arbitrageApi } from '../api/arbitrageApi';
import type { TradeSummary } from '../types';

export function PnLChart() {
  const [summary, setSummary] = useState<TradeSummary | null>(null);
  const [chartData, setChartData] = useState<{ time: string; pnl: number }[]>([]);

  useEffect(() => {
    const fetch = async () => {
      try {
        const s = await arbitrageApi.getTradeSummary();
        setSummary(s);
        const trades = await arbitrageApi.getTrades(200);
        setChartData(
          trades
            .reverse()
            .reduce<{ time: string; pnl: number }[]>(
              (acc, t) => {
                const last = acc.length > 0 ? acc[acc.length - 1].pnl : 0;
                acc.push({
                  time: new Date(t.executedAt).toLocaleTimeString(),
                  pnl: last + t.netProfit,
                });
                return acc;
              },
              []
            )
        );
      } catch {}
    };
    fetch();
    const id = setInterval(fetch, 10000);
    return () => clearInterval(id);
  }, []);

  return (
    <div className="card">
      <h3>📈 P&L Acumulado</h3>
      {summary && (
        <div style={{ display: 'flex', gap: 20, marginBottom: 16 }}>
          <div>
            <div className="label">PnL Total</div>
            <div className={`mono ${summary.totalPnl >= 0 ? 'green' : 'red'}`} style={{ fontSize: 18, fontWeight: 700 }}>
              {summary.totalPnl >= 0 ? '+' : ''}${summary.totalPnl.toFixed(3)}
            </div>
          </div>
          <div>
            <div className="label">Trades</div>
            <div className="mono" style={{ fontSize: 18, fontWeight: 700 }}>{summary.totalTrades}</div>
          </div>
          <div>
            <div className="label">Win Rate</div>
            <div className="mono green" style={{ fontSize: 18, fontWeight: 700 }}>
              {(summary.winRate * 100).toFixed(1)}%
            </div>
          </div>
        </div>
      )}
      <ResponsiveContainer width="100%" height={200}>
        <AreaChart data={chartData}>
          <defs>
            <linearGradient id="pnlGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--green)" stopOpacity={0.3} />
              <stop offset="100%" stopColor="var(--green)" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
          <XAxis dataKey="time" stroke="var(--text-dim)" fontSize={11} />
          <YAxis stroke="var(--text-dim)" fontSize={11} />
          <Tooltip
            contentStyle={{
              background: 'var(--surface2)',
              border: '1px solid var(--border)',
              borderRadius: 8,
              color: 'var(--text)',
              fontSize: 13,
            }}
          />
          <Area
            type="monotone"
            dataKey="pnl"
            stroke="var(--green)"
            strokeWidth={2}
            fill="url(#pnlGrad)"
          />
        </AreaChart>
      </ResponsiveContainer>
      {chartData.length === 0 && (
        <div style={{ textAlign: 'center', padding: 20, color: 'var(--text-dim)' }}>
          Sin datos — los trades aparecerán aquí cuando se ejecuten
        </div>
      )}
    </div>
  );
}
