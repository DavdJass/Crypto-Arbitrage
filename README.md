# ₿ Crypto Arbitrage Bot

Sistema de detección y **simulación de arbitraje BTC/USDT en tiempo real** entre **10 exchanges simultáneos**. Construido con **.NET 8** (backend) y **React 18** (frontend), comunicados por **SignalR WebSockets**.

> **Modo simulación:** todas las operaciones son virtuales. Los balances parten de 50,000 USDT + 0.5 BTC por exchange y se actualizan con fees, slippage y costos de retiro reales.

---

## Índice

1. [¿Qué es el arbitraje de Bitcoin?](#qué-es-el-arbitraje-de-bitcoin)
2. [Arquitectura del sistema](#arquitectura-del-sistema)
3. [Fórmula de rentabilidad neta](#fórmula-de-rentabilidad-neta)
4. [Exchanges soportados](#exchanges-soportados)
5. [Dashboard en tiempo real](#dashboard-en-tiempo-real)
6. [Requisitos previos](#requisitos-previos)
7. [Ejecución local](#ejecución-local)
8. [Ejecución con Docker](#ejecución-con-docker)
9. [Despliegue en la nube](#despliegue-en-la-nube)
10. [Configuración avanzada](#configuración-avanzada)
11. [API REST](#api-rest)
12. [SignalR — eventos en vivo](#signalr--eventos-en-vivo)
13. [Estrategia de riesgo](#estrategia-de-riesgo)
14. [Arbitraje triangular](#arbitraje-triangular)
15. [Tests](#tests)
16. [Stack técnico](#stack-técnico)

---

## ¿Qué es el arbitraje de Bitcoin?

El precio de BTC/USDT no es idéntico en todos los exchanges en el mismo instante. Las diferencias surgen por:

- **Liquidez fragmentada**: cada exchange tiene su propio order book.
- **Latencia de arbitrajistas**: las ineficiencias duran milisegundos antes de que el mercado las corrija.
- **Velocidad de ejecución**: quien detecta y ejecuta más rápido captura el spread.

**Estrategia básica:**
```
1. Detectar que BTC está más barato en Exchange A que en Exchange B
2. Comprar BTC en A  (al precio Ask de A)
3. Vender BTC en B   (al precio Bid de B)
4. El spread − fees − slippage = profit neto
```

Este bot escanea **90 pares posibles** (10 exchanges × 9 combinaciones) en cada actualización del market, selecciona las oportunidades con retorno neto positivo y simula la ejecución.

---

## Arquitectura del sistema

### Vista general

```
┌──────────────────────────────────────────────────────────────────────┐
│                         CRYPTO ARBITRAGE BOT                         │
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                    MARKET DATA FEEDS                         │    │
│  │                                                              │    │
│  │  Binance WS ──┐                                             │    │
│  │  Kraken  WS ──┤                                             │    │
│  │  Bybit   WS ──┤                                             │    │
│  │  Coinbase WS──┼──► OrderBookAggregator ──► Channel<Book>   │    │
│  │  OKX     WS ──┤         │                                   │    │
│  │  Bitfinex WS──┤    MemoryCache                              │    │
│  │  Gate.io WS ──┤    (top of book                             │    │
│  │  Bitstamp WS──┤     por exchange)                           │    │
│  │  Gemini  WS ──┤                                             │    │
│  │  KuCoin REST──┘  (REST polling si WS falla)                 │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                           │                                          │
│                           ▼                                          │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                  DETECTION ENGINE                            │    │
│  │                                                              │    │
│  │  ArbitrageDetectorService                                    │    │
│  │  ├─ Evalúa N×N pares por cada tick del market              │    │
│  │  ├─ Prioriza por NetProfit desc                             │    │
│  │  ├─ Throttle: observed → máx 5 pares/ciclo, 1/5s por par   │    │
│  │  └─ Emite via Channel + SignalR                             │    │
│  │                                                              │    │
│  │  TriangularArbitrageService                                  │    │
│  │  └─ BTC→USDT→ETH→BTC dentro del mismo exchange              │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                           │                                          │
│                           ▼                                          │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                 EXECUTION ENGINE                             │    │
│  │                                                              │    │
│  │  TradeExecutorService                                        │    │
│  │  ├─ Verifica CircuitBreaker                                 │    │
│  │  ├─ Verifica fondos (USDT + BTC por exchange)               │    │
│  │  ├─ Calcula volumen real (limitado por liquidez del book)    │    │
│  │  ├─ Ejecuta ExecutionSettlement (fees + slippage exactos)    │    │
│  │  ├─ Actualiza WalletManager                                  │    │
│  │  └─ Guarda TradeResult en repositorio                       │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                           │                                          │
│              ┌────────────┴────────────┐                            │
│              ▼                         ▼                            │
│  ┌─────────────────┐       ┌──────────────────────┐                │
│  │   PERSISTENCE   │       │    SIGNALR HUB        │                │
│  │                 │       │                        │                │
│  │  SQLite (prod)  │       │  OrderBookUpdated      │                │
│  │  In-Memory (dev)│       │  OpportunityFound      │                │
│  │                 │       │  TradeExecuted         │                │
│  │  - TradeResult  │       │  TriangularOpportunity │                │
│  │  - StoredOpp    │       └──────────┬─────────────┘               │
│  └─────────────────┘                  │                             │
└──────────────────────────────────────────────────────────────────────┘
                                        │ WebSocket
                                        ▼
                     ┌──────────────────────────────────┐
                     │        REACT DASHBOARD           │
                     │                                  │
                     │  HeroStats  (PnL, win rate,      │
                     │             último trade)        │
                     │  OrderBookPanel  (flash prices)  │
                     │  OpportunitiesTable + Detail     │
                     │  WalletPanel  (ΔUSDT, ΔBTC)      │
                     │  CircuitBreakerPanel             │
                     │  PnLChart                        │
                     │  TradesPanel                     │
                     └──────────────────────────────────┘
```

### Capas del backend

```
ArbitrageBot.API          ← Controllers REST + SignalR Hub + Swagger
    │
ArbitrageBot.Application  ← Lógica de negocio (sin dependencias de infra)
    │   ArbitrageDetectorService
    │   TradeExecutorService
    │   ProfitCalculator
    │   WalletManager
    │   CircuitBreaker
    │   TriangularArbitrageService
    │
ArbitrageBot.Infrastructure ← Adaptadores externos
    │   Feeds: BinanceFeed, KrakenFeed, BybitFeed, CoinbaseFeed,
    │          OKXFeed, BitfinexFeed, KuCoinFeed, GateIoFeed,
    │          BitstampFeed, GeminiFeed
    │   Cache: MemoryOrderBookCache
    │   Persistence: SqliteTradeRepository, SqliteOpportunityRepository
    │                InMemoryTradeRepository, InMemoryOpportunityRepository
    │
ArbitrageBot.Domain       ← Entidades, interfaces, configuración
        Models: OrderBook, ArbitrageOpportunity, StoredOpportunity,
                TradeResult, ExecutionSettlement, CircuitBreakerState
        Interfaces: IOrderBookFeed, IOrderBookAggregator,
                    ITradeRepository, IOpportunityRepository,
                    IWalletManager
```

### Pipeline de datos (sin locks)

```
WS/REST Feed
     │
     ▼
IOrderBookFeed.ConnectLoopAsync()   ← BackgroundService por exchange
     │  publica cada tick via:
     ▼
OrderBookAggregator.PublishAsync()
     │  escribe en:
     ▼
Channel<OrderBook>   (UnboundedChannel, SingleWriter=false)
     │  lee:
     ▼
ArbitrageDetectorService.ExecuteAsync()
     │  evalúa N×N pares, escribe en:
     ▼
Channel<ArbitrageOpportunity>   (UnboundedChannel, SingleReader=true)
     │  lee:
     ▼
TradeExecutorService.ExecuteAsync()
     │
     ├──► WalletManager.TryExecuteArbitrage()
     ├──► ITradeRepository.SaveAsync()
     └──► SignalR: TradeExecuted
```

---

## Fórmula de rentabilidad neta

El `ProfitCalculator` aplica la fórmula completa antes de decidir si una oportunidad es ejecutable:

```
Paso 1 — Volumen real (limitado por liquidez)
  volumen = min(askVolume_exchange_A, bidVolume_exchange_B, MaxVolumeBtc)

Paso 2 — Costo de compra (en exchange A)
  buyCost = askPrice × volumen × (1 + tradingFee_A)

Paso 3 — Ingreso de venta (en exchange B)
  sellProceeds = bidPrice × volumen × (1 − tradingFee_B)

Paso 4 — Slippage estimado
  slippage = askPrice × volumen × SlippagePct   (0.05% por defecto)

Paso 5 — Fee de retiro
  withdrawalFee = WithdrawalFeeUsdt_A   (fee fijo en USDT del exchange origen)

Paso 6 — Profit neto
  netProfit = sellProceeds − buyCost − slippage − withdrawalFee

Paso 7 — Retorno neto
  returnPct = netProfit / buyCost

  → EJECUTAR si returnPct > MinReturnPct (0.2% por defecto)
```

**Ejemplo numérico real:**
```
BTC Ask en OKX:      $74,177.60   (fee 0.08%)
BTC Bid en Bitstamp: $74,178.98   (fee 0.30%)
Volumen:             0.0675 BTC   (limitado por liquidez)

buyCost       = 74,177.60 × 0.0675 × 1.0008  = $5,007.97
sellProceeds  = 74,178.98 × 0.0675 × 0.9970  = $4,990.01  ← fees restan
slippage      = 74,177.60 × 0.0675 × 0.0005  = $2.50
withdrawalFee = $1.50  (OKX)

netProfit = 4,990.01 − 5,007.97 − 2.50 − 1.50 = −$21.96   ← Spread insuficiente
```

> En mercados reales eficientes las oportunidades con retorno neto positivo son raras y duran milisegundos. El bot las detecta en cuanto llega el tick y las marca como "detectable".

---

## Exchanges soportados

| Exchange  | Conexión          | Par        | Fee Taker | Fee Retiro |
|-----------|-------------------|------------|-----------|------------|
| Binance   | WebSocket (100ms) | BTCUSDT    | 0.10%     | $2.00      |
| Kraken    | WebSocket         | XBT/USDT   | 0.16%     | $3.50      |
| Bybit     | WebSocket         | BTCUSDT    | 0.10%     | $1.50      |
| Coinbase  | WebSocket         | BTC-USDT   | 0.40%     | $2.50      |
| OKX       | WebSocket         | BTC-USDT   | 0.08%     | $1.50      |
| Bitfinex  | WebSocket         | tBTCUST    | 0.10%     | $2.00      |
| KuCoin    | REST polling 1.5s | BTC-USDT   | 0.10%     | $1.00      |
| Gate.io   | WebSocket         | BTC_USDT   | 0.10%     | $1.50      |
| Bitstamp  | WebSocket         | btcusdt    | 0.30%     | $3.00      |
| Gemini    | WebSocket         | BTCUSDT    | 0.35%     | $2.50      |

> Si un WebSocket falla, el feed hace fallback automático a REST con backoff exponencial (2→4→8→...→30s).

---

## Dashboard en tiempo real

El frontend React muestra:

| Sección | Descripción |
|---------|-------------|
| **Hero Stats** | PnL sesión, Win Rate, último trade ejecutado, cronómetro de sesión |
| **Order Books** | Bid/Ask con animación de flash verde/rojo en cada cambio de precio |
| **Rendimiento** | Feeds activos, latencia promedio, estado de conexiones |
| **Circuit Breaker** | Estado, barra de riesgo, tiempo de reapertura |
| **Oportunidades** | Tabla live de las 30 más recientes (observed: upsert por par, detected: nuevas primeras) |
| **Detalle de Oportunidad** | Modal con desglose completo: fees, slippage, motivo de decisión |
| **Arbitraje Triangular** | Oportunidades BTC→USDT→ETH→BTC detectadas |
| **PnL Chart** | Gráfico histórico de ganancias/pérdidas acumuladas |
| **Wallets** | Balance USDT y BTC por exchange + delta vs balance inicial |
| **Trades** | Historial de las últimas 100 operaciones simuladas |

---

## Requisitos previos

| Herramienta | Versión mínima | Necesario para |
|-------------|---------------|----------------|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 | Backend |
| [Node.js](https://nodejs.org/) | 20 LTS | Frontend |
| [Docker](https://docs.docker.com/get-docker/) | 24+ | Despliegue completo (opcional) |

---

## Ejecución local

### 1. Clonar el repositorio

```bash
git clone https://github.com/tu-usuario/Crypto-Arbitrage.git
cd Crypto-Arbitrage
```

### 2. Backend (.NET 8)

```bash
cd dotnetcore-cryptoarbitrage/ArbitrageBot.API

# Restaurar dependencias y ejecutar
dotnet run --launch-profile http
```

El backend arranca en **http://localhost:5152**

- Swagger UI: http://localhost:5152/swagger
- SignalR Hub: ws://localhost:5152/hubs/arbitrage

> Por defecto usa almacenamiento **en memoria** (no necesita base de datos). Para persistir en SQLite ver [Configuración avanzada](#configuración-avanzada).

### 3. Frontend (React + Vite)

En otra terminal:

```bash
cd applicationweb-cryptoarbitrage

# Instalar dependencias
npm install

# Iniciar en modo desarrollo (proxy automático a :5152)
npm run dev
```

Dashboard en: **http://localhost:3000**

### Verificar que funciona

```
✅ En el dashboard aparece el badge "EN VIVO" (verde)
✅ Los Order Books muestran precios de BTC/USDT de varios exchanges
✅ La sección Oportunidades muestra filas con status "Observada" o "Detectable"
✅ Los Wallets muestran $50,000 USDT y 0.5 BTC por exchange
```

---

## Ejecución con Docker

La forma más sencilla de levantar todo el sistema:

```bash
cd Crypto-Arbitrage
docker compose up -d
```

Esto levanta:
- **Frontend** → http://localhost:3000
- **Backend + API** → http://localhost:5152/swagger
- **SQLite** persistido en volumen Docker

Para ver logs en tiempo real:

```bash
docker compose logs -f
```

Para detener:

```bash
docker compose down
```

### Variables de entorno en Docker

El `docker-compose.yml` admite override vía `.env` en la raíz del proyecto:

```bash
# .env (crear en Crypto-Arbitrage/)
SQLITE_PATH=/data/arbitrage.db
Security__ApiKey=mi-clave-secreta
VITE_BACKEND_URL=http://localhost:5152
VITE_API_KEY=mi-clave-secreta
```

---

## Despliegue en la nube

### Backend → Railway (recomendado)

```bash
# Instalar Railway CLI
npm install -g @railway/cli

railway login
cd dotnetcore-cryptoarbitrage
railway init
railway up
```

Variables a configurar en Railway Dashboard:

```
ASPNETCORE_ENVIRONMENT   = Production
ASPNETCORE_URLS          = http://+:8080
SQLITE_PATH              = /data/arbitrage.db
Security__ApiKey         = <tu-clave-secreta>
```

### Frontend → Vercel

```bash
cd applicationweb-cryptoarbitrage
npx vercel --prod
```

Variables en Vercel Dashboard:

```
VITE_BACKEND_URL = https://tu-backend.railway.app
VITE_API_KEY     = <tu-clave-secreta>
```

### Frontend → Netlify

```bash
cd applicationweb-cryptoarbitrage
npm run build
npx netlify deploy --prod --dir=dist
```

### CORS en producción

En `appsettings.json`, actualiza `AllowedOrigins` con la URL de tu frontend:

```json
"Security": {
  "AllowedOrigins": "https://tu-frontend.vercel.app"
}
```

---

## Configuración avanzada

Toda la configuración vive en `dotnetcore-cryptoarbitrage/ArbitrageBot.API/appsettings.json`:

### Parámetros de arbitraje

```json
"Arbitrage": {
  "MaxVolumeBtc": 0.1,       // Máximo BTC por operación (gestión de riesgo)
  "MinReturnPct": 0.002,     // Retorno mínimo para ejecutar: 0.2%
  "SlippagePct": 0.0005,     // Slippage estimado: 0.05%
  "NetworkLatencyMs": 150    // Latencia de red estimada (informativo)
}
```

### Circuit Breaker

```json
"CircuitBreaker": {
  "WindowSize": 5,            // Ventana de evaluación: últimos N trades
  "MaxLossesBeforeOpen": 3,  // Abrir si >= 3 de 5 son pérdidas
  "CooldownSeconds": 30      // Tiempo de pausa antes de reanudar
}
```

### Balances iniciales (por exchange)

```json
"Wallets": {
  "Wallets": {
    "Binance": { "InitialUsdt": 50000, "InitialBtc": 0.5 },
    "Kraken":  { "InitialUsdt": 50000, "InitialBtc": 0.5 }
    // ... (10 exchanges)
  }
}
```

### Parámetros de exchanges

```json
"Exchanges": {
  "Exchanges": {
    "Binance": {
      "Fee": 0.0010,               // 0.10% fee de trading
      "WithdrawalFeeUsdt": 2.00,   // Fee de retiro en USDT
      "Symbol": "btcusdt",         // Par a monitorear
      "WebSocketUrl": "wss://...", // Endpoint WebSocket
      "RestUrl": "https://..."     // Endpoint REST (fallback)
    }
  }
}
```

### Variables de entorno

| Variable | Plataforma | Descripción |
|----------|-----------|-------------|
| `SQLITE_PATH` | Backend | Ruta del archivo SQLite. Sin esto → in-memory |
| `Security__ApiKey` | Backend | API Key para autenticación REST y SignalR |
| `Security__AllowedOrigins` | Backend | CORS: URL del frontend en producción |
| `VITE_BACKEND_URL` | Frontend (build) | URL del backend en producción |
| `VITE_API_KEY` | Frontend (build) | API Key (misma que el backend) |

---

## API REST

Todos los endpoints requieren el header:
```
X-API-Key: arbibot-secret-key-2024
```

### Order Books

```
GET /api/orderbooks
```
```json
[
  {
    "exchangeId": "Binance",
    "bestBid": 74177.50,
    "bestAsk": 74178.20,
    "bidVolume": 1.2500,
    "askVolume": 0.8300,
    "timestamp": "2026-05-30T21:00:00Z"
  }
]
```

### Oportunidades detectadas

```
GET /api/opportunities?limit=100
```
```json
[
  {
    "id": "3fa85f64-...",
    "buyExchange": "OKX",
    "sellExchange": "Bitstamp",
    "askPrice": 74177.60,
    "bidPrice": 74178.98,
    "volume": 0.0675,
    "netProfit": 22.93,
    "returnPct": 0.00458,
    "status": "detected",
    "reason": null,
    "detectedAt": "2026-05-30T21:00:01Z"
  }
]
```

**Status posibles:**
| Status | Significado |
|--------|-------------|
| `detected` | Spread positivo y retorno > 0.2% → ejecutable |
| `observed` | Spread positivo pero retorno insuficiente |
| `executed` | Trade simulado completado |
| `skipped` | Descartada (fondos insuficientes, circuito abierto, etc.) |

### Trades ejecutados

```
GET /api/trades?limit=50
GET /api/trades/summary
```

### Wallets

```
GET /api/status/wallets
```
```json
[
  { "exchangeId": "Binance", "usdtBalance": 50023.45, "btcBalance": 0.4998 }
]
```

### Estado del sistema

```
GET /api/status/connections      ← Estado de feeds WebSocket
GET /api/status/circuit-breaker  ← Estado del circuit breaker
```

### Swagger

Documentación interactiva completa en: `http://localhost:5152/swagger`

---

## SignalR — eventos en vivo

Conexión al hub:
```
ws://localhost:5152/hubs/arbitrage
Header: Authorization: Bearer <api-key>
```

### Eventos disponibles

| Evento | Payload | Descripción |
|--------|---------|-------------|
| `OrderBookUpdated` | `OrderBook` | Cada tick de precio de un exchange |
| `OpportunityFound` | `StoredOpportunity` | Oportunidad detectada u observada |
| `TradeExecuted` | `TradeResult` | Trade simulado completado |
| `TriangularOpportunity` | `TriangularOpportunity` | Oportunidad triangular BTC→USDT→ETH→BTC |

### Ejemplo con JavaScript

```javascript
import * as signalR from '@microsoft/signalr';

const conn = new signalR.HubConnectionBuilder()
  .withUrl('http://localhost:5152/hubs/arbitrage', {
    accessTokenFactory: () => 'arbibot-secret-key-2024'
  })
  .withAutomaticReconnect()
  .build();

conn.on('OrderBookUpdated', (book) => {
  console.log(`${book.exchangeId}: bid=${book.bestBid} ask=${book.bestAsk}`);
});

conn.on('OpportunityFound', (opp) => {
  console.log(`${opp.status}: ${opp.buyExchange}→${opp.sellExchange} net=${opp.netProfit}`);
});

await conn.start();
```

---

## Estrategia de riesgo

### Circuit Breaker

El circuit breaker protege contra condiciones de mercado adversas o errores sistemáticos:

```
Estado CERRADO (normal) → monitorea resultados de trades
      │
      │  Si >= 3 pérdidas en los últimos 5 trades
      ▼
Estado ABIERTO → pausa toda detección y ejecución por 30 segundos
      │
      │  Al expirar el cooldown
      ▼
Estado CERRADO (reanuda automáticamente)
```

La barra de riesgo en el dashboard muestra el nivel actual (verde → amarillo → rojo).

### Órdenes parciales

El volumen de cada trade se limita al **mínimo de**:
- Volumen disponible en el ask del exchange de compra
- Volumen disponible en el bid del exchange de venta
- `MaxVolumeBtc` configurado (0.1 BTC por defecto)

Esto evita ejecutar más de lo que el mercado puede absorber.

### Verificación de fondos

Antes de cada ejecución simulada:
1. ¿Hay suficiente USDT en el exchange de compra?
2. ¿Hay suficiente BTC en el exchange de venta?
3. Si no → trade marcado como `skipped` con razón `insufficient_balance`

---

## Arbitraje triangular

Estrategia adicional dentro de un **mismo exchange**:

```
BTC → USDT → ETH → BTC

Paso 1: Vender BTC al Bid de BTC/USDT
Paso 2: Comprar ETH con USDT al Ask real de ETH/USDT
Paso 3: Vender ETH → BTC implícito (ETH_bid / BTC_ask)

Umbral: retorno > 0.15% (sin withdrawal fee entre exchanges)
```

El servicio consulta ETH/USDT en Binance, Kraken y Bybit vía REST cada 2 segundos y lo combina con los order books BTC/USDT en vivo.

---

## Tests

```bash
cd dotnetcore-cryptoarbitrage
dotnet test
```

Tests incluidos:

| Suite | Qué verifica |
|-------|-------------|
| `CircuitBreakerTests` | Apertura por umbral de pérdidas, ventana deslizante, cooldown automático, estado en tiempo real |
| `ProfitCalculatorTests` | Cálculo de fees, slippage, withdrawal fee, retorno neto, umbral de ejecución |

Para ver output detallado:

```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## Stack técnico

### Backend

| Tecnología | Uso |
|-----------|-----|
| .NET 8 / C# | Framework principal |
| ASP.NET Core Web API | REST endpoints |
| SignalR | WebSocket push al frontend |
| `System.Threading.Channels` | Pipeline asíncrono sin locks |
| `System.Net.WebSockets` | Conexiones WS a exchanges |
| SQLite + Dapper | Persistencia en producción |
| xUnit | Tests unitarios |
| Swagger / Swashbuckle | Documentación API |

### Frontend

| Tecnología | Uso |
|-----------|-----|
| React 18 + TypeScript | UI reactiva |
| Vite | Build tool |
| `@microsoft/signalr` | Cliente WebSocket |
| Recharts | Gráfico PnL |
| CSS custom (glassmorphism) | Diseño dark/light mode |

### Infraestructura

| Herramienta | Uso |
|-----------|-----|
| Docker + Docker Compose | Contenedores locales |
| Railway | Deploy backend (recomendado) |
| Vercel / Netlify | Deploy frontend |

---

## Estructura del proyecto

```
Crypto-Arbitrage/
├── docker-compose.yml
├── README.md                            ← Este archivo
│
├── dotnetcore-cryptoarbitrage/          ← Backend .NET 8
│   ├── ArbitrageBot.API/
│   │   ├── Controllers/
│   │   │   ├── OrderBooksController.cs
│   │   │   ├── OpportunitiesController.cs
│   │   │   ├── TradesController.cs
│   │   │   └── StatusController.cs
│   │   ├── Hubs/ArbitrageHub.cs
│   │   ├── Program.cs
│   │   └── appsettings.json            ← Config exchanges, arbitraje, CB
│   │
│   ├── ArbitrageBot.Application/
│   │   └── Services/
│   │       ├── ArbitrageDetectorService.cs
│   │       ├── TradeExecutorService.cs
│   │       ├── ProfitCalculator.cs
│   │       ├── WalletManager.cs
│   │       ├── CircuitBreaker.cs
│   │       └── TriangularArbitrageService.cs
│   │
│   ├── ArbitrageBot.Infrastructure/
│   │   ├── Feeds/
│   │   │   ├── BinanceFeed.cs
│   │   │   ├── KrakenFeed.cs
│   │   │   └── ... (10 feeds)
│   │   ├── Persistence/
│   │   │   ├── SqliteTradeRepository.cs
│   │   │   ├── SqliteOpportunityRepository.cs
│   │   │   └── InMemory*.cs
│   │   └── Cache/MemoryOrderBookCache.cs
│   │
│   ├── ArbitrageBot.Domain/
│   │   ├── Models/
│   │   │   ├── OrderBook.cs
│   │   │   ├── ArbitrageOpportunity.cs
│   │   │   ├── StoredOpportunity.cs
│   │   │   ├── TradeResult.cs
│   │   │   ├── ExecutionSettlement.cs
│   │   │   └── CircuitBreakerState.cs
│   │   └── Interfaces/
│   │       ├── IOrderBookFeed.cs
│   │       ├── ITradeRepository.cs
│   │       ├── IOpportunityRepository.cs
│   │       └── IWalletManager.cs
│   │
│   └── ArbitrageBot.Tests/
│       ├── CircuitBreakerTests.cs
│       └── ProfitCalculatorTests.cs
│
└── applicationweb-cryptoarbitrage/     ← Frontend React
    ├── src/
    │   ├── components/
    │   │   ├── Dashboard.tsx
    │   │   ├── HeroStats.tsx
    │   │   ├── OrderBookPanel.tsx
    │   │   ├── OpportunitiesTable.tsx
    │   │   ├── OpportunityDetail.tsx
    │   │   ├── WalletPanel.tsx
    │   │   ├── CircuitBreakerPanel.tsx
    │   │   ├── ConnectionHealth.tsx
    │   │   ├── TradesPanel.tsx
    │   │   └── PnLChart.tsx
    │   ├── hooks/
    │   │   ├── useSignalR.ts           ← Gestión SignalR + historial REST
    │   │   └── useTheme.ts
    │   ├── api/arbitrageApi.ts         ← Cliente REST
    │   └── types/index.ts              ← TypeScript interfaces
    └── vite.config.ts                  ← Proxy dev → :5152
```

---

<div align="center">

**Crypto Arbitrage Bot** — Detección de oportunidades BTC/USDT en tiempo real entre 10 exchanges

*Sistema de simulación — no realiza operaciones reales en ningún exchange*

</div>
