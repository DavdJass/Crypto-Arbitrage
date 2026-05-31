import { useEffect, useMemo, useState } from 'react';
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import { arbitrageApi } from '../api/arbitrageApi';
import type { TradeResult, TradeSummary } from '../types';

function buildChartData(trades: TradeResult[]) {
  const executed = [...trades]
    .filter(t => t.status === 'executed' || t.status === 'executed_partial')
    .sort((a, b) => new Date(a.executedAt).getTime() - new Date(b.executedAt).getTime());

  let cumulative = 0;
  return executed.map(t => {
    cumulative += t.netProfit;
    return {
      time: new Date(t.executedAt).toLocaleTimeString(),
      pnl: cumulative,
    };
  });
}

export function PnLChart({ trades }: { trades: TradeResult[] }) {
  const [summary, setSummary] = useState<TradeSummary | null>(null);

  const chartData = useMemo(() => buildChartData(trades), [trades]);
  const lastPnl = chartData.length > 0 ? chartData[chartData.length - 1].pnl : 0;
  const strokeColor = lastPnl >= 0 ? 'var(--green)' : 'var(--red)';

  useEffect(() => {
    const doFetch = () => arbitrageApi.getTradeSummary().then(setSummary).catch(() => {});
    doFetch();
    const id = setInterval(doFetch, 10000);
    return () => clearInterval(id);
  }, []);

  return (
    <div className="card">
      <h3>📈 P&L Acumulado</h3>
      {summary && (
        <div style={{ display: 'flex', gap: 20, marginBottom: 16 }}>
          <div>
            <div className="label">PnL Total</div>
            <div
              className={`mono ${summary.totalPnl >= 0 ? 'green' : 'red'}`}
              style={{ fontSize: 18, fontWeight: 700 }}
            >
              {summary.totalPnl >= 0 ? '+' : ''}${summary.totalPnl.toFixed(3)}
            </div>
          </div>
          <div>
            <div className="label">Trades</div>
            <div className="mono" style={{ fontSize: 18, fontWeight: 700 }}>
              {summary.totalTrades}
            </div>
          </div>
          <div>
            <div className="label">Win Rate</div>
            <div
              className={`mono ${summary.winRate >= 0.5 ? 'green' : 'red'}`}
              style={{ fontSize: 18, fontWeight: 700 }}
            >
              {(summary.winRate * 100).toFixed(1)}%
            </div>
          </div>
        </div>
      )}
      {chartData.length === 0 ? (
        <div style={{ textAlign: 'center', padding: 40, color: 'var(--text-dim)' }}>
          Sin trades ejecutados — el gráfico aparecerá aquí
        </div>
      ) : (
        <ResponsiveContainer width="100%" height={200}>
          <AreaChart data={chartData}>
            <defs>
              <linearGradient id="pnlGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={strokeColor} stopOpacity={0.3} />
                <stop offset="100%" stopColor={strokeColor} stopOpacity={0} />
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
              stroke={strokeColor}
              strokeWidth={2}
              fill="url(#pnlGrad)"
            />
          </AreaChart>
        </ResponsiveContainer>
      )}
    </div>
  );
}
