import type { TradeResult } from '../types';

export function TradesPanel({ trades }: { trades: TradeResult[] }) {
  return (
    <div>
      <div className="section-header">
        <h2>📋 Trades Ejecutados ({trades.length})</h2>
      </div>
      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Hora</th>
              <th>Compra</th>
              <th>Venta</th>
              <th>Volume</th>
              <th>PnL</th>
              <th>Return %</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {trades.length === 0 && (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '20px' }}>
                  Sin trades aún — esperando oportunidades rentables
                </td>
              </tr>
            )}
            {trades.map((t) => (
              <tr key={t.id}>
                <td className="mono dim">
                  {new Date(t.executedAt).toLocaleTimeString()}
                </td>
                <td>{t.buyExchange}</td>
                <td>{t.sellExchange}</td>
                <td className="mono">{t.volume.toFixed(4)}</td>
                <td className={`mono ${t.isProfit ? 'green' : 'red'}`}>
                  ${t.netProfit.toFixed(3)}
                </td>
                <td className={`mono ${t.isProfit ? 'green' : 'red'}`}>
                  {(t.returnPct * 100).toFixed(3)}%
                </td>
                <td>
                  <span
                    className={`badge ${
                      t.status === 'executed'
                        ? 'badge-ok'
                        : t.status === 'executed_partial'
                        ? 'badge-warn'
                        : 'badge-err'
                    }`}
                  >
                    {t.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
