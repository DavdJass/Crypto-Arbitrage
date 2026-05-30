import type { ArbitrageOpportunity } from '../types';

export function OpportunitiesTable({
  ops,
  onClear,
}: {
  ops: ArbitrageOpportunity[];
  onClear: () => void;
}) {
  const executable = (op: ArbitrageOpportunity) =>
    op.netProfit > 0 && op.returnPct > 0.002 && op.bidPrice > op.askPrice;

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
              <th>Compra</th>
              <th>Vende</th>
              <th>Precio Ask</th>
              <th>Precio Bid</th>
              <th>Volumen</th>
              <th>Profit Neto</th>
              <th>Retorno</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {ops.slice(0, 25).map((op, i) => {
              const exec = executable(op);
              return (
                <tr key={i} className={exec ? 'row-executable' : ''}>
                  <td><span className="gold">{op.buyExchange}</span></td>
                  <td><span className="gold">{op.sellExchange}</span></td>
                  <td className="mono red">${op.askPrice.toFixed(2)}</td>
                  <td className="mono green">${op.bidPrice.toFixed(2)}</td>
                  <td className="mono">{op.volume.toFixed(4)}</td>
                  <td className={`mono ${op.netProfit > 0 ? 'green' : 'red'}`}>
                    ${op.netProfit.toFixed(3)}
                  </td>
                  <td className={`mono ${op.returnPct > 0 ? 'green' : 'red'}`}>
                    {(op.returnPct * 100).toFixed(3)}%
                  </td>
                  <td>{exec ? '✅' : '❌'}</td>
                </tr>
              );
            })}
            {ops.length === 0 && (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '32px 16px', color: 'var(--text-dim)' }}>
                  Esperando datos del mercado...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
