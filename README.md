# Flash Sale API

A high-concurrency flash sale inventory system built with Go, demonstrating how to prevent overselling when thousands of users compete for limited stock simultaneously.

## The Problem

When a flash sale starts, hundreds of requests arrive in milliseconds. A naive read-then-write approach leads to a race condition:

```
[User A] reads remaining_qty = 1  ←┐
[User B] reads remaining_qty = 1  ←┘ both see stock available
[User A] UPDATE remaining_qty = 0     ← both succeed
[User B] UPDATE remaining_qty = -1    ← oversold!
```

## The Solution: Atomic UPDATE

Instead of SELECT → check → UPDATE (3 steps, race-prone), use a single atomic SQL statement:

```sql
UPDATE campaign
SET remaining_qty = remaining_qty - $qty, updated_at = NOW()
WHERE id            = $campaign_id
  AND status        = 1               -- campaign must be active
  AND remaining_qty >= $qty           -- must have enough stock
  AND start_at      <= NOW()          -- within campaign window
  AND end_at        >= NOW()
```

- `RowsAffected() == 0` → sold out or campaign invalid → return 409
- `RowsAffected() == 1` → success → insert order record
- DB `CHECK (remaining_qty >= 0)` is the final safety net

No `SELECT FOR UPDATE`, no application-level locks — the database serializes competing updates at the row level.

## Tech Stack

| Component | Choice |
|-----------|--------|
| Language  | Go 1.22+ |
| Framework | Gin |
| Database  | PostgreSQL 16 |
| Cache     | Redis 7 (reserved for Phase 2) |
| Deploy    | Docker Compose |
| Load Test | k6 / Go concurrency test |
| API Docs  | Swagger / OpenAPI 3.0 |

## API

Swagger UI: `http://localhost:8080/swagger/index.html`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/products/:id` | Get product info |
| GET | `/api/v1/campaigns` | List all campaigns with remaining stock |
| GET | `/api/v1/campaigns/:id` | Get campaign detail |
| POST | `/api/v1/orders` | **Place order (concurrency core)** |

### POST /api/v1/orders

```json
// Request
{
  "campaign_id":     "00000000-0000-0000-0000-000000000002",
  "user_id":         "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "qty":             1,
  "idempotency_key": "unique-uuid-per-request"
}

// 201 Created
{
  "order_id":   "d6aa7191-...",
  "status":     1,
  "unit_price": 29900,
  "qty":        1,
  "subtotal":   29900,
  "created_at": "2024-03-29T10:00:00Z"
}

// 409 Conflict (sold out)
{
  "error_code": "OUT_OF_STOCK",
  "message":    "Sorry, the campaign items are sold out or the campaign is not active."
}
```

`idempotency_key` prevents duplicate orders on network retry — same key returns the original result without creating a second order.

## Quick Start

```bash
# 1. Start PostgreSQL + Redis (migration runs automatically)
docker compose up -d db redis

# 2. Copy env and run server
cp .env.example .env
go run ./cmd/server

# 3. Open Swagger UI
open http://localhost:8080/swagger/index.html
```

Seed data is loaded automatically: 1 product (iPhone 15 Pro) + 1 active campaign with 100 units at ¥29,900.

## Database Schema

```
product ──< campaign ──< flash_order
  (1:N)         (1:N)
```

- **product**: base item, original price
- **campaign**: flash sale event — owns `remaining_qty` (the contested resource)
- **flash_order**: purchase record with price snapshot

`remaining_qty` on `campaign` is the single critical resource all concurrent requests compete for. Keeping it on the campaign (not the product) isolates the concurrency scope.

## Concurrency Test Results

500 goroutines simultaneously competing for 100 units:

```
go test ./internal/repository/... -v -run TestCreateOrder_Concurrency

Results: success=100  failed=400  total=500
DB state: remaining_qty=0  confirmed_orders=100
PASS (0.71s)
```

Exactly 100 orders succeed. `remaining_qty` reaches 0, never negative.

## Project Structure

```
FlashSales/
├── cmd/server/main.go          # Entry point, Gin routing
├── internal/
│   ├── handler/                # HTTP layer (request binding, response)
│   ├── service/                # Business logic (idempotency check)
│   └── repository/             # DB queries (atomic UPDATE lives here)
├── migrations/001_init.sql     # Schema + seed data
├── docs/                       # Auto-generated Swagger files
├── k6/load_test.js             # k6 load test script (500 VUs)
└── docker-compose.yml          # app + postgres + redis
```

## Design Decisions

**Why Atomic UPDATE over SELECT FOR UPDATE?**
`SELECT FOR UPDATE` holds a row lock while the transaction runs. Under high concurrency, this causes lock queuing and timeout cascades. The atomic UPDATE holds the lock only for the duration of the update itself — much shorter critical section.

**Why no User table?**
`user_id` is a UUID passed by the caller. This simplifies load testing (k6 can generate random UUIDs without pre-seeding users) and keeps the concurrency scope focused on the campaign inventory.

**Why no OrderItem?**
Flash sales are single-item checkouts by design — the urgency UX is incompatible with cart management. `OrderItem` would be the natural extension for cart-based purchases.

**Phase 2 (not implemented): Redis pre-deduction**
For 10,000+ TPS, move the first inventory check to Redis (Lua script for atomicity), write to DB asynchronously. This introduces an eventual consistency tradeoff that requires a reconciliation process.
