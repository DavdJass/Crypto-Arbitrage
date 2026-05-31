import type { StoredOpportunity } from '../types';

const statusConfig: Record<string, { label: string; color: string }> = {
  executed:   { label: 'Ejecutada', color: 'var(--green)' },
  detected:   { label: 'Detectada', color: 'var(--gold, #f0b90b)' },
  observed:   { label: 'Observada', color: 'var(--text-dim)' },
  skipped:    { label: 'Omitida',   color: 'var(--red)' },
  circuit_open: { label: 'Circuit Open', color: 'var(--red)' },
  insufficient_balance: { label: 'Sin Fondos', color: 'var(--red)' },
  low_volume: { label: 'Vol. bajo', color: 'var(--red)' },
  low_profit: { label: 'Profit bajo', color: 'var(--red)' },
};

function Row({ label, value, positive }: { label: string; value: React.ReactNode; positive?: boolean }) {
  const color =
    positive === true  ? 'var(--green)' :
    positive === false ? 'var(--red)'   : undefined;

  return (
    <div style={{
      display: 'flex',
      justifyContent: 'space-between',
      padding: '6px 0',
      borderBottom: '1px solid var(--border)',
      gap: 8,
    }}>
      <span style={{ color: 'var(--text-dim)', fontSize: 13 }}>{label}</span>
      <span className="mono" style={{ fontSize: 13, color }}>{value}</span>
    </div>
  );
}

export function OpportunityDetail({
  opp,
  onClose,
}: {
  opp: StoredOpportunity;
  onClose: () => void;
}) {
  const st = statusConfig[opp.status] ?? { label: opp.status, color: 'var(--text-dim)' };
  const spread = opp.bidPrice - opp.askPrice;
  const spreadPct = opp.askPrice > 0 ? (spread / opp.askPrice) * 100 : 0;

  // Estimates de fees (matching backend defaults: 0.1% taker × 2 legs)
  const tradingFeesEst = opp.askPrice * opp.volume * 0.001 * 2;
  const slippageEst = opp.askPrice * opp.volume * 0.0005; // ~0.05% proxy
  const grossProfit = spread * opp.volume;

  return (
    <div style={{
      position: 'fixed', inset: 0,
      background: 'rgba(0,0,0,0.6)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      zIndex: 1000,
      backdropFilter: 'blur(4px)',
    }} onClick={onClose}>
      <div
        style={{
          background: 'var(--card)',
          border: '1px solid var(--border)',
          borderRadius: 12,
          padding: 24,
          minWidth: 360,
          maxWidth: 480,
          width: '90vw',
          boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
        }}
        onClick={e => e.stopPropagation()}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h3 style={{ margin: 0, fontSize: 16 }}>
            🎯 {opp.buyExchange} → {opp.sellExchange}
          </h3>
          <button
            onClick={onClose}
            style={{
              background: 'none', border: 'none', cursor: 'pointer',
              color: 'var(--text-dim)', fontSize: 20, lineHeight: 1, padding: 4,
            }}
          >×</button>
        </div>

        <div style={{ marginBottom: 12 }}>
          <span
            className="badge"
            style={{
              background: `${st.color}22`,
              color: st.color,
              border: `1px solid ${st.color}55`,
              borderRadius: 6,
              padding: '3px 10px',
              fontSize: 12,
            }}
          >
            {st.label}
          </span>
          {opp.reason && (
            <span className="dim" style={{ marginLeft: 8, fontSize: 12 }}>
              ({opp.reason.replace(/_/g, ' ')})
            </span>
          )}
        </div>

        <Row label="Precio Ask (compra)" value={`$${opp.askPrice.toLocaleString(undefined, { minimumFractionDigits: 2 })}`} />
        <Row label="Precio Bid (venta)"  value={`$${opp.bidPrice.toLocaleString(undefined, { minimumFractionDigits: 2 })}`} />
        <Row label="Spread bruto"        value={`$${spread.toFixed(2)} (${spreadPct.toFixed(4)}%)`} positive={spread > 0} />
        <Row label="Volumen ejecutado"   value={`${opp.volume.toFixed(6)} BTC`} />
        <Row label="Profit bruto est."   value={`$${grossProfit.toFixed(4)}`} />
        <Row label="Fees trading est."   value={`-$${tradingFeesEst.toFixed(4)}`} positive={false} />
        <Row label="Slippage est."       value={`-$${slippageEst.toFixed(4)}`} positive={false} />
        <Row
          label="Profit neto"
          value={`${opp.netProfit >= 0 ? '+' : ''}$${opp.netProfit.toFixed(4)}`}
          positive={opp.netProfit >= 0}
        />
        <Row
          label="Retorno neto"
          value={`${(opp.returnPct * 100).toFixed(4)}%`}
          positive={opp.returnPct > 0}
        />
        <Row label="Detectada" value={new Date(opp.detectedAt).toLocaleString()} />
      </div>
    </div>
  );
}
