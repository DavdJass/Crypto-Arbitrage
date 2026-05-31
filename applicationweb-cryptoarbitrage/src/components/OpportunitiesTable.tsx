import { useState } from 'react';
import type { StoredOpportunity } from '../types';
import { OpportunityDetail } from './OpportunityDetail';

const statusBadge: Record<string, { label: string; cls: string }> = {
  executed:             { label: '✅ Ejecutada',   cls: 'badge-ok'   },
  detected:             { label: '🎯 Detectable',  cls: 'badge-warn' },
  observed:             { label: '👁 Observada',   cls: 'badge-dim'  },
  skipped:              { label: '⏭ Omitida',     cls: 'badge-dim'  },
  circuit_open:         { label: '⚡ Circuit',     cls: 'badge-err'  },
  insufficient_balance: { label: '💸 Sin Fondos',  cls: 'badge-err'  },
  low_volume:           { label: '📉 Vol.Bajo',    cls: 'badge-err'  },
  low_profit:           { label: '📉 Profit↓',    cls: 'badge-err'  },
};

export function OpportunitiesTable({
  ops,
  onClear,
}: {
  ops: StoredOpportunity[];
  onClear: () => void;
}) {
  const [selected, setSelected] = useState<StoredOpportunity | null>(null);

  return (
    <div>
      <div className="section-header">
        <h2>🎯 Oportunidades <span>({ops.length})</span></h2>
        <button onClick={onClear} className="btn-sm">Limpiar</button>
      </div>

      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Estado</th>
              <th>Compra</th>
              <th>Vende</th>
              <th>Ask</th>
              <th>Bid</th>
              <th>Spread</th>
              <th>Volumen</th>
              <th>Profit Neto</th>
              <th>Retorno</th>
            </tr>
          </thead>
          <tbody>
            {ops.slice(0, 50).map((op) => {
              const sb = statusBadge[op.status] ?? { label: op.status, cls: 'badge-dim' };
              const spread = op.bidPrice - op.askPrice;
              const spreadPct = op.askPrice > 0 ? (spread / op.askPrice) * 100 : 0;
              const isExec = op.status === 'executed';
              const isDetectable = op.status === 'detected';

              return (
                <tr
                  key={op.id}
                  className={isExec ? 'row-executable' : isDetectable ? 'row-detectable' : ''}
                  style={{ cursor: 'pointer' }}
                  onClick={() => setSelected(op)}
                  title="Clic para ver desglose"
                >
                  <td>
                    <span className={`badge ${sb.cls}`} style={{ fontSize: 11, padding: '2px 6px' }}>
                      {sb.label}
                    </span>
                  </td>
                  <td><span className="gold">{op.buyExchange}</span></td>
                  <td><span className="gold">{op.sellExchange}</span></td>
                  <td className="mono red">${op.askPrice.toFixed(2)}</td>
                  <td className="mono green">${op.bidPrice.toFixed(2)}</td>
                  <td className={`mono ${spread > 0 ? 'green' : 'red'}`}>
                    ${spread.toFixed(2)}
                    <span className="dim"> ({spreadPct.toFixed(3)}%)</span>
                  </td>
                  <td className="mono">{op.volume.toFixed(4)}</td>
                  <td className={`mono ${op.netProfit >= 0 ? 'green' : 'red'}`}>
                    {op.netProfit >= 0 ? '+' : ''}${op.netProfit.toFixed(3)}
                  </td>
                  <td className={`mono ${op.returnPct > 0 ? 'green' : 'red'}`}>
                    {(op.returnPct * 100).toFixed(3)}%
                  </td>
                </tr>
              );
            })}

            {ops.length === 0 && (
              <tr>
                <td colSpan={9} style={{ textAlign: 'center', padding: '32px 16px', color: 'var(--text-dim)' }}>
                  Esperando datos del mercado…
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {selected && (
        <OpportunityDetail opp={selected} onClose={() => setSelected(null)} />
      )}
    </div>
  );
}
