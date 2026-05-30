-- Migration 001: Crear tabla de trades ejecutados
CREATE TABLE IF NOT EXISTS trades (
    id              UUID PRIMARY KEY,
    buy_exchange    VARCHAR(20) NOT NULL,
    sell_exchange   VARCHAR(20) NOT NULL,
    volume          DECIMAL(18,8) NOT NULL,
    net_profit      DECIMAL(18,8) NOT NULL,
    return_pct      DECIMAL(18,8) NOT NULL,
    is_profit       BOOLEAN NOT NULL,
    status          VARCHAR(30) NOT NULL,
    executed_at     TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_trades_executed_at ON trades (executed_at DESC);
