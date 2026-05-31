import type { StoredOpportunity } from '../types';

const statusConfig: Record<string, { label: string; color: string }> = {
  executed:   { label: 'Ejecutada', color: 'var(--green)' },
  detected:   { label: 'Detectable', color: 'var(--gold)' },
  observed:   { label: 'Observada', color: 'var(--text-dim)' },
  skipped:    { label: 'Omitida', color: 'var(--red)' },
  circuit_open: { label: 'Circuit abierto', color: 'var(--red)' },
  insufficient_balance: { label: 'Sin fondos', color: 'var(--red)' },
  low_volume: { label: 'Vol. bajo', color: 'var(--red)' },
  low_profit: { label: 'Profit bajo', color: 'var(--red)' },
};

function DecisionRow({
  label,
  value,
  positive,
  negative,
}: {
  label: string;
  value: string;
  positive?: boolean;
  negative?: boolean;
}) {
  const color = positive ? 'var(--green)' : negative ? 'var(--red)' : undefined;
  return (
    <div className="decision-row">
      <span style={{ color: 'var(--text-dim)' }}>{label}</span>
      <span className="mono" style={{ color, fontWeight: 600 }}>{value}</span>
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

  const tradingFeesEst = opp.askPrice * opp.volume * 0.001 * 2;
  const slippageEst = opp.askPrice * opp.volume * 0.0005;
  const latencyEst = opp.askPrice * opp.volume * 0.15 * 0.001;
  const grossProfit = spread * opp.volume;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-panel" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 className="modal-title">Análisis de oportunidad</h3>
            <p className="modal-route">
              <span className="gold">{opp.buyExchange}</span>
              <span className="exchange-pair-arrow"> → </span>
              <span className="gold">{opp.sellExchange}</span>
            </p>
          </div>
          <button type="button" className="modal-close" onClick={onClose} aria-label="Cerrar">
            ×
          </button>
        </div>

        <span
          className="badge"
          style={{
            background: `${st.color}18`,
            color: st.color,
            border: `1px solid ${st.color}44`,
            marginBottom: 16,
          }}
        >
          {st.label}
        </span>

        <DecisionRow label="Ask (compra)" value={`$${opp.askPrice.toLocaleString(undefined, { maximumFractionDigits: 2 })}`} />
        <DecisionRow label="Bid (venta)" value={`$${opp.bidPrice.toLocaleString(undefined, { maximumFractionDigits: 2 })}`} />
        <DecisionRow
          label="Spread"
          value={`$${spread.toFixed(2)} (${spreadPct.toFixed(4)}%)`}
          positive={spread > 0}
        />
        <DecisionRow label="Volumen" value={`${opp.volume.toFixed(6)} BTC`} />
        <DecisionRow label="Profit bruto (est.)" value={`$${grossProfit.toFixed(2)}`} />
        <DecisionRow label="Fees trading (est.)" value={`−$${tradingFeesEst.toFixed(2)}`} negative />
        <DecisionRow label="Slippage (est.)" value={`−$${slippageEst.toFixed(2)}`} negative />
        <DecisionRow label="Latencia (est.)" value={`−$${latencyEst.toFixed(2)}`} negative />

        <div className={`decision-summary ${opp.netProfit >= 0 ? 'positive' : 'negative'}`}>
          <div style={{ fontSize: 12, color: 'var(--text-dim)', marginBottom: 6 }}>Profit neto estimado</div>
          <div
            className="decision-profit"
            style={{ color: opp.netProfit >= 0 ? 'var(--green)' : 'var(--red)' }}
          >
            {opp.netProfit >= 0 ? '+' : ''}${opp.netProfit.toFixed(4)}
            <span style={{ fontSize: 14, fontWeight: 500, marginLeft: 10, color: 'var(--text-dim)' }}>
              {(opp.returnPct * 100).toFixed(4)}%
            </span>
          </div>
          {opp.reason && (
            <div className="dim" style={{ marginTop: 8, fontSize: 12 }}>
              {opp.reason.replace(/_/g, ' ')}
            </div>
          )}
        </div>

        <div className="dim" style={{ marginTop: 12, fontSize: 11 }}>
          {new Date(opp.detectedAt).toLocaleString()}
        </div>
      </div>
    </div>
  );
}
