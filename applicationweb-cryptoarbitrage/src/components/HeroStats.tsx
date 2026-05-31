import { useEffect, useState } from 'react';
import { arbitrageApi } from '../api/arbitrageApi';
import type { TradeSummary, TradeResult } from '../types';

const EXCHANGE_COUNT = 10;
const INITIAL_USDT_PER_EXCHANGE = 50_000;
const TOTAL_INITIAL_CAPITAL_USDT = EXCHANGE_COUNT * INITIAL_USDT_PER_EXCHANGE;

function formatPnl(pnl: number): string {
  const sign = pnl >= 0 ? '+' : '';
  return `${sign}$${pnl.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function Kpi({
  label,
  value,
  sub,
  positive,
  featured,
  icon,
}: {
  label: string;
  value: string;
  sub?: string;
  positive?: boolean;
  featured?: boolean;
  icon?: string;
}) {
  const color =
    positive === undefined
      ? 'var(--text)'
      : positive
      ? 'var(--green)'
      : 'var(--red)';

  return (
    <div className={`hero-kpi ${featured ? 'hero-kpi-primary' : ''}`}>
      {icon && <div className="hero-kpi-icon">{icon}</div>}
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
  const totalPct =
    TOTAL_INITIAL_CAPITAL_USDT > 0 ? (pnl / TOTAL_INITIAL_CAPITAL_USDT) * 100 : 0;

  return (
    <div className="hero-stats">
      <Kpi
        featured
        icon="◆"
        label="PnL Total"
        value={formatPnl(pnl)}
        sub={`${totalPct >= 0 ? '+' : ''}${totalPct.toFixed(4)}% · capital $${(TOTAL_INITIAL_CAPITAL_USDT / 1000).toFixed(0)}k`}
        positive={pnl >= 0}
      />
      <Kpi
        icon="◎"
        label="Win Rate"
        value={summary ? `${(summary.winRate * 100).toFixed(1)}%` : '—'}
        sub={summary ? `${summary.winningTrades} ganadores de ${summary.totalTrades}` : 'Sin trades aún'}
        positive={summary ? summary.winRate >= 0.5 : undefined}
      />
      <Kpi
        icon="⇄"
        label="Último Trade"
        value={
          lastTrade
            ? `${lastTrade.netProfit >= 0 ? '+' : ''}$${lastTrade.netProfit.toFixed(3)}`
            : '—'
        }
        sub={
          lastTrade
            ? `${lastTrade.buyExchange} → ${lastTrade.sellExchange}`
            : 'Esperando ejecución…'
        }
        positive={lastTrade ? lastTrade.netProfit >= 0 : undefined}
      />
      <Kpi
        icon="⏱"
        label="Sesión"
        value={`${hh}:${mm}:${ss}`}
        sub={`${summary?.totalTrades ?? 0} trades simulados`}
      />
    </div>
  );
}
