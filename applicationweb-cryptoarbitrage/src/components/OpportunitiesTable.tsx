import type { ArbitrageOpportunity } from '../types';

export function OpportunitiesTable({
  ops,
  onClear,
}: {
  ops: ArbitrageOpportunity[];
  onClear: () => void;
}) {
  const executable = (op: ArbitrageOpportunity) => {
    return op.netProfit > 0 && op.returnPct > 0.002 && op.bidPrice > op.askPrice;
  };

  return (
    <div>
      <div className="section-header">
        <h2>🎯 Oportunidades ({ops.length})</h2>
        <button onClick={onClear} className="btn-sm">Limpiar</button>
      </div>
      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Compra</th>
              <th>Vende</th>
              <th>Ask</th>
              <th>Bid</th>
              <th>Net Profit</th>
              <th>Return %</th>
              <th>Ejecutable</th>
            </tr>
          </thead>
          <tbody>
            {ops.slice(0, 30).map((op, i) => {
              const exec = executable(op);
              return (
                <tr key={i} className={exec ? 'row-executable' : ''}>
                  <td>{op.buyExchange}</td>
                  <td>{op.sellExchange}</td>
                  <td className="mono">${op.askPrice.toFixed(2)}</td>
                  <td className="mono">${op.bidPrice.toFixed(2)}</td>
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
          </tbody>
        </table>
      </div>
    </div>
  );
}
