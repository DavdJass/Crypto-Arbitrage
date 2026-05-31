import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { TradeSummary, TradeResult } from '../types';

const INITIAL_CAPITAL_USDT = 50_000;

function formatPnl(pnl: number): string {
  const sign = pnl >= 0 ? '+' : '';
  return `${sign}$${pnl.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function Kpi({
  label,
  value,
  sub,
  positive,
}: {
  label: string;
  value: string;
  sub?: string;
  positive?: boolean;
}) {
  const color =
    positive === undefined
      ? 'var(--text)'
      : positive
      ? 'var(--green)'
      : 'var(--red)';

  return (
    <div className="hero-kpi">
      <div className="hero-kpi-label">{label}</div>
      <div className="hero-kpi-value" style={{ color }}>{value}</div>
      {sub && <div className="hero-kpi-sub">{sub}</div>}
    </div>
  );
}

export function HeroStats({ trades }: { trades: TradeResult[] }) {
  const [summary, setSummary] = useState<TradeSummary | null>(null);
  const [sessionStart] = useState(() => Date.now());
  const [elapsed, setElapsed] = useState(0);

  useEffect(() => {
    const tick = setInterval(() => setElapsed(Date.now() - sessionStart), 1000);
    return () => clearInterval(tick);
  }, [sessionStart]);

  useEffect(() => {
    const doFetch = () => arbitrageApi.getTradeSummary().then(setSummary).catch(() => {});
    doFetch();
    const id = setInterval(doFetch, 5000);
    return () => clearInterval(id);
  }, []);

  const lastTrade = trades[0] ?? null;

  const elapsedSec = Math.floor(elapsed / 1000);
  const hh = Math.floor(elapsedSec / 3600).toString().padStart(2, '0');
  const mm = Math.floor((elapsedSec % 3600) / 60).toString().padStart(2, '0');
  const ss = (elapsedSec % 60).toString().padStart(2, '0');

  const pnl = summary?.totalPnl ?? 0;
  const totalPct = INITIAL_CAPITAL_USDT > 0 ? (pnl / INITIAL_CAPITAL_USDT) * 100 : 0;

  return (
    <div className="hero-stats">
      <Kpi
        label="PnL Sesión"
        value={formatPnl(pnl)}
        sub={`${totalPct >= 0 ? '+' : ''}${totalPct.toFixed(4)}% sobre capital`}
        positive={pnl >= 0}
      />
      <Kpi
        label="Win Rate"
        value={summary ? `${(summary.winRate * 100).toFixed(1)}%` : '—'}
        sub={summary ? `${summary.winningTrades}/${summary.totalTrades} trades ganadores` : 'Sin trades aún'}
        positive={summary ? summary.winRate >= 0.5 : undefined}
      />
      <Kpi
        label="Último Trade"
        value={
          lastTrade
            ? `${lastTrade.netProfit >= 0 ? '+' : ''}$${lastTrade.netProfit.toFixed(3)}`
            : '—'
        }
        sub={
          lastTrade
            ? `${lastTrade.buyExchange} → ${lastTrade.sellExchange} · ${(lastTrade.returnPct * 100).toFixed(3)}%`
            : 'Esperando ejecución…'
        }
        positive={lastTrade ? lastTrade.netProfit >= 0 : undefined}
      />
      <Kpi
        label="Tiempo de Sesión"
        value={`${hh}:${mm}:${ss}`}
        sub={`${summary?.totalTrades ?? 0} trades ejecutados`}
      />
    </div>
  );
}
