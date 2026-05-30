# 🚀 Crypto Arbitrage Bot

Sistema de detección y simulación de **arbitraje de Bitcoin en tiempo real** entre múltiples exchanges (Binance, Kraken, Bybit). Desarrollado en **.NET 8** con arquitectura de 4 capas, WebSockets, SignalR y pipeline de alto rendimiento basado en `System.Threading.Channels`.

---

## 🏗️ Arquitectura

```
┌─────────────────────────────────────────────────────────┐
│                     ArbitrageBot.API                    │
│  REST Controllers  ·  SignalR Hub  ·  Swagger UI       │
├─────────────────────────────────────────────────────────┤
│                 ArbitrageBot.Application                │
│  ArbitrageDetector  ·  TradeExecutor  ·  CircuitBreaker │
│  ProfitCalculator   ·  WalletManager                   │
├─────────────────────────────────────────────────────────┤
│                ArbitrageBot.Infrastructure              │
│  BinanceFeed  ·  KrakenFeed  ·  BybitFeed             │
│  FeedHealthTracker  ·  MemoryOrderBookCache            │
│  TradeRepository (Postgres o In-Memory)                │
├─────────────────────────────────────────────────────────┤
│                   ArbitrageBot.Domain                   │
│  Interfaces  ·  Models  ·  Configuration (Options)     │
└─────────────────────────────────────────────────────────┘
```

### Pipeline de datos (Channel-based, sin locks)

```
WS Binance ──┐
WS Kraken  ──┼──► OrderBookAggregator ──► Channel<OrderBook>
WS Bybit   ──┘         │                          │
                        ▼                          ▼
                  MemoryCache           ArbitrageDetector
                                         (evalúa N×N pares,
                                          prioriza por profit)
                                                │
                                                ▼
                                        Channel<ArbitrageOpportunity>
                                                │
                                                ▼
                                         TradeExecutor
                                   (órdenes parciales por liquidez,
                                    withdrawal fees, circuit breaker)
                                                │
                                        ▼              ▼
                                   Postgres/InMem    SignalR Hub
                                   TradeRepository   (push frontend)
```

---

## ⚡ Características

| Feature | Detalle |
|---------|---------|
| **WebSockets** | Binance, Kraken, Bybit con datos en vivo |
| **REST fallback** | Si WS falla → polling REST cada 2s |
| **Reconexión** | Backoff exponencial (2→4→8...30s) |
| **Detección N×N** | Evalúa todas las combinaciones de exchanges |
| **Priorización** | Oportunidades ordenadas por NetProfit desc |
| **Fórmula completa** | Trading fees + Withdrawal fees + Slippage |
| **Órdenes parciales** | Ajusta volumen según liquidez disponible |
| **Circuit Breaker** | 3 de 5 trades negativos → pausa 30s |
| **SignalR** | Push en tiempo real al frontend |
| **Thread-safe** | `Channels` + `ConcurrentDictionary` + `lock` |

---

## 🔌 Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/orderbooks` | Order books en vivo de todos los exchanges |
| GET | `/api/trades?limit=50` | Últimos trades ejecutados |
| GET | `/api/trades/summary` | PnL total, win rate, total trades |
| GET | `/api/status/wallets` | Balances simulados por exchange |
| GET | `/api/status/connections` | Estado de las conexiones WebSocket |
| GET | `/api/status/circuit-breaker` | Estado del circuit breaker |
| WS | `/hubs/arbitrage` | SignalR: `OrderBookUpdated`, `OpportunityFound`, `TradeExecuted` |
| GET | `/swagger` | Swagger UI |

---

## 🚀 Quick Start

### Requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL (opcional — usa almacenamiento en memoria por defecto)

```bash
# Clonar el repo
git clone <repo-url>
cd dotnetcore-cryptoarbitrage

# Restaurar y compilar
dotnet restore
dotnet build

# Ejecutar
cd ArbitrageBot.API
dotnet run --launch-profile http
```

Abrir navegador en: **http://localhost:5152/swagger**

---

## ⚙️ Configuración

Toda la configuración está en `ArbitrageBot.API/appsettings.json`:

```json
{
  "Exchanges": {
    "Exchanges": {
      "Binance": {
        "Fee": 0.0010,           // 0.1% trading fee
        "WithdrawalFeeUsdt": 2.00, // Fee de retiro
        "Symbol": "btcusdt"
      }
    }
  },
  "Arbitrage": {
    "MaxVolumeBtc": 0.1,        // Cap de riesgo
    "MinReturnPct": 0.002,      // Umbral mínimo 0.2%
    "SlippagePct": 0.0005       // Slippage estimado 0.05%
  },
  "CircuitBreaker": {
    "WindowSize": 5,
    "MaxLossesBeforeOpen": 3,
    "CooldownSeconds": 30
  }
}
```

---

## 🗄️ PostgreSQL (opcional)

Por defecto, los trades se almacenan en **memoria** (`InMemoryTradeRepository`). Para usar PostgreSQL:

1. Cambiar en `ArbitrageBot.Infrastructure/DependencyInjection.cs`:
```csharp
// Reemplazar:
services.AddSingleton<ITradeRepository, InMemoryTradeRepository>();
// Por:
services.AddSingleton<ITradeRepository, TradeRepository>();
```

2. Configurar connection string en `appsettings.json`.

---

## 🧠 Fórmula de rentabilidad

```
buyCost    = askPrice × volume × (1 + tradingFeeBuy)
sellGain   = bidPrice × volume × (1 − tradingFeeSell)
slippage   = askPrice × volume × slippagePct
netProfit  = sellGain − buyCost − slippage − withdrawalFee
returnPct  = netProfit / buyCost
→ ejecutar si returnPct > minReturnPct (0.2%)
```

---

## 🛡️ Estrategia de riesgo

- **Volumen máximo por trade:** 0.1 BTC (configurable)
- **Circuit Breaker:** Si 3 de los últimos 5 trades resultan negativos, se pausa la detección 30 segundos
- **Órdenes parciales:** El volumen se ajusta automáticamente a la liquidez disponible en ambos order books
- **Verificación de fondos:** Antes de ejecutar, se verifica que haya USDT y BTC suficientes

---

## 📦 Stack

- **Backend:** .NET 8, C#, ASP.NET Core Web API
- **Tiempo real:** SignalR + System.Threading.Channels
- **DB/almacenamiento:** PostgreSQL (Dapper) o In-Memory
- **Cache:** ConcurrentDictionary (sin locks)
- **Frontend:** React + Vite + Recharts (pendiente)
- **Deploy:** Railway (back) + Vercel (front)
