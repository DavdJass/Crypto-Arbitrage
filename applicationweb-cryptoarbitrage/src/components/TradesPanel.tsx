import type { TradeResult } from '../types';

export function TradesPanel({ trades }: { trades: TradeResult[] }) {
  return (
    <div>
      <div className="section-header">
        <h2>📋 Historial de Trades <span>({trades.length})</span></h2>
      </div>
      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Hora</th>
              <th>Compra → Venta</th>
              <th>Volumen</th>
              <th>PnL</th>
              <th>Return</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {trades.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: 'center', padding: '32px 16px', color: 'var(--text-dim)' }}>
                  Sin ejecuciones aún — el bot está monitoreando el mercado
                </td>
              </tr>
            )}
            {trades.map((t) => (
              <tr key={t.id}>
                <td className="mono dim">
                  {new Date(t.executedAt).toLocaleTimeString()}
                </td>
                <td>
                  <span className="gold">{t.buyExchange}</span>
                  <span className="dim"> → </span>
                  <span className="gold">{t.sellExchange}</span>
                </td>
                <td className="mono">{t.volume.toFixed(4)} BTC</td>
                <td className={`mono ${t.isProfit ? 'green' : 'red'}`}>
                  {t.isProfit ? '+' : ''}${t.netProfit.toFixed(3)}
                </td>
                <td className={`mono ${t.isProfit ? 'green' : 'red'}`}>
                  {(t.returnPct * 100).toFixed(3)}%
                </td>
                <td>
                  <span className={`badge ${
                    t.status === 'executed' ? 'badge-ok' :
                    t.status === 'executed_partial' ? 'badge-warn' : 'badge-err'
                  }`}>
                    {t.status === 'executed' ? '✓ Ejecutado' :
                     t.status === 'executed_partial' ? '⚠ Parcial' : '✗ Fallido'}
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
