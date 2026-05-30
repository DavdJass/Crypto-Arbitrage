import type { OrderBook } from '../types';

const exchangeColors: Record<string, string> = {
  Binance: '#F3BA2F',
  Kraken: '#5741D9',
  Bybit: '#F7A600',
};

export function OrderBookPanel({ books }: { books: Record<string, OrderBook> }) {
  return (
    <div className="grid">
      {Object.entries(books).map(([id, book]) => {
        const spread = book.bestAsk - book.bestBid;
        const spreadPct = book.bestBid > 0 ? (spread / book.bestBid) * 100 : 0;
        const color = exchangeColors[id] || '#666';

        return (
          <div key={id} className="card" style={{ borderTop: `3px solid ${color}` }}>
            <h3 style={{ color }}>{id}</h3>
            <div className="card-row">
              <span className="label">Bid</span>
              <span className="mono green">${book.bestBid.toLocaleString()}</span>
            </div>
            <div className="card-row">
              <span className="label">Ask</span>
              <span className="mono red">${book.bestAsk.toLocaleString()}</span>
            </div>
            <div className="card-row">
              <span className="label">Spread</span>
              <span className="mono">${spread.toFixed(2)}</span>
            </div>
            <div className="card-row">
              <span className="label">Spread %</span>
              <span className="mono">{spreadPct.toFixed(4)}%</span>
            </div>
            <div className="card-row dim">
              <span>Vol Bid: {book.bidVolume.toFixed(4)}</span>
              <span>Vol Ask: {book.askVolume.toFixed(4)}</span>
            </div>
          </div>
        );
      })}
      {Object.keys(books).length === 0 && (
        <div className="card">Conectando feeds...</div>
      )}
    </div>
  );
}
