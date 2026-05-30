import { useEffect, useState } from 'react';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { arbitrageApi } from '../api/arbitrageApi';
import type { TradeResult, TradeSummary } from '../types';

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
        <div style={{ display: 'flex', gap: 16, marginBottom: 12 }}>
          <span className={`mono ${summary.totalPnl >= 0 ? 'green' : 'red'}`}>
            PnL: ${summary.totalPnl.toFixed(3)}
          </span>
          <span className="dim">Trades: {summary.totalTrades}</span>
          <span className="dim">
            Win Rate: {(summary.winRate * 100).toFixed(1)}%
          </span>
        </div>
      )}
      <ResponsiveContainer width="100%" height={180}>
        <AreaChart data={chartData}>
          <CartesianGrid strokeDasharray="3 3" stroke="#333" />
          <XAxis dataKey="time" stroke="#999" fontSize={11} />
          <YAxis stroke="#999" fontSize={11} />
          <Tooltip
            contentStyle={{
              background: '#1a1a2e',
              border: '1px solid #333',
              borderRadius: 6,
              color: '#fff',
            }}
          />
          <Area
            type="monotone"
            dataKey="pnl"
            stroke="#22c55e"
            fill="#22c55e20"
            strokeWidth={2}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
