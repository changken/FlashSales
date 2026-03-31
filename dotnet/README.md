# Flash Sale API — ASP.NET 10 版本

這是 [Flash Sale API](../README.md) 的 ASP.NET 10 Web API 實作，與 Go 版本共用相同的資料庫 schema 和 API 契約，展示如何在 .NET 生態系中實現同等的高併發防超賣機制。

## 核心問題：Race Condition

搶購開始時，大量請求在毫秒內同時抵達。naive 的先讀後寫做法會造成競態條件：

```
[User A] SELECT remaining_qty = 1  ←┐
[User B] SELECT remaining_qty = 1  ←┘  兩者都看到有庫存
[User A] UPDATE remaining_qty = 0      ← 兩者都成功
[User B] UPDATE remaining_qty = -1     ← 超賣！
```

## 解決方案：Atomic UPDATE

用單一 SQL 語句原子性地執行「檢查 + 扣減」：

```sql
UPDATE campaign
SET remaining_qty = remaining_qty - @qty,
    updated_at    = NOW()
WHERE id            = @campaignId
  AND status        = 1               -- 活動必須是進行中
  AND remaining_qty >= @qty           -- 必須有足夠庫存
  AND start_at      <= NOW()          -- 在活動時間內
  AND end_at        >= NOW()
  AND deleted_at    IS NULL
```

- `ExecuteNonQueryAsync()` 回傳 `0` → 庫存不足或活動無效 → 回傳 409
- `ExecuteNonQueryAsync()` 回傳 `1` → 成功 → 建立訂單
- DB `CHECK (remaining_qty >= 0)` 是最後一道防線

不需要 `SELECT FOR UPDATE`，不需要應用層鎖，由 PostgreSQL 的 row-level locking 在 UPDATE 時序列化競爭請求。

## Tech Stack

| 元件 | 選擇 |
|------|------|
| 語言 / 框架 | C# / ASP.NET Core 10 Web API（Controller） |
| ORM（讀取） | Dapper 2.x |
| DB Driver（寫入） | Npgsql 10（raw SQL，用於 atomic UPDATE） |
| 資料庫 | PostgreSQL 16 |
| Cache | Redis 7（保留給 Phase 2） |
| API 文件 | Swagger / Swashbuckle |
| 部署 | Docker Compose |
| 測試 | xUnit + 500 Task 併發測試 |

## API 端點

Swagger UI：`http://localhost:8081/swagger`

| Method | Path | 說明 |
|--------|------|------|
| GET | `/api/v1/products/{id}` | 取得商品資訊 |
| GET | `/api/v1/campaigns` | 列出所有活動（含剩餘庫存） |
| GET | `/api/v1/campaigns/{id}` | 取得活動詳情 |
| POST | `/api/v1/orders` | **下單搶購（核心併發端點）** |

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
  "created_at": "2024-03-29 10:00:00.000000 +00:00"
}

// 409 Conflict（庫存不足）
{
  "error_code": "OUT_OF_STOCK",
  "message":    "Sorry, the campaign items are sold out or the campaign is not active."
}
```

`idempotency_key` 防止網路重試造成重複下單——相同的 key 會直接回傳原始訂單，不會建立第二筆。

## 快速啟動

```bash
# 啟動（port 8081，避免與 Go 版本的 8080 衝突）
cd dotnet
docker compose up -d --build

# 開啟 Swagger UI
open http://localhost:8081/swagger
```

種子資料會自動載入：1 個商品（iPhone 15 Pro）+ 1 個進行中的活動，100 件，售價 ¥29,900。

### 本機開發（不用 Docker）

需要先有 PostgreSQL（可沿用 Go 版本的 DB container）：

```bash
cd dotnet/src/FlashSales.Api
DATABASE_URL="Host=localhost;Port=5432;Database=flashsales;Username=flashsales;Password=flashsales" dotnet run
```

## 專案結構

```
dotnet/
├── FlashSales.sln
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── src/FlashSales.Api/
│   ├── Program.cs                    # 組合根：DI、JSON 序列化、路由
│   ├── appsettings.json
│   ├── Models/
│   │   ├── Product.cs
│   │   ├── Campaign.cs               # 含 CampaignWithProduct
│   │   ├── FlashOrder.cs
│   │   └── Enums.cs                  # ProductStatus, CampaignStatus, OrderStatus
│   ├── Dtos/
│   │   ├── CreateOrderRequest.cs
│   │   ├── CreateOrderResponse.cs
│   │   └── ErrorResponse.cs
│   ├── Repositories/
│   │   ├── ProductRepository.cs      # Dapper 查詢
│   │   ├── CampaignRepository.cs     # Dapper 查詢
│   │   └── OrderRepository.cs        # ★ raw Npgsql + NpgsqlTransaction
│   ├── Services/
│   │   ├── ProductService.cs
│   │   ├── CampaignService.cs
│   │   └── OrderService.cs           # 冪等性檢查
│   ├── Controllers/
│   │   ├── ProductsController.cs
│   │   ├── CampaignsController.cs
│   │   └── OrdersController.cs
│   └── Infrastructure/
│       ├── DbConnectionFactory.cs
│       ├── OutOfStockException.cs
│       └── ServiceCollectionExtensions.cs
└── tests/FlashSales.Tests/
    └── OrderConcurrencyTests.cs      # 500 Task 併發測試
```

## 併發測試

500 個 Task 同時搶購 100 件商品（對應 Go 版本的 500 goroutine 測試）：

```bash
cd dotnet
DATABASE_URL="Host=localhost;Port=5433;Database=flashsales;Username=flashsales;Password=flashsales" dotnet test -v
```

預期結果：

```
成功下單：100
失敗（庫存不足）：400
DB remaining_qty：0
confirmed 訂單數：100
```

庫存正好歸零，不超賣，不少賣。

## 資料庫 Schema

共用 Go 版本的 migration 檔案 (`../migrations/001_init.sql`)，不重複維護。

```
product ──< campaign ──< flash_order
  (1:N)         (1:N)
```

- **product**：商品基本資料、原價
- **campaign**：搶購活動，持有 `remaining_qty`（所有併發請求競爭的資源）
- **flash_order**：訂單，含下單時的價格快照

## 設計決策

### 為什麼用 raw Npgsql 而非 EF Core？

EF Core 的樂觀並發控制（`RowVersion` / `ConcurrencyToken`）在高頻更新下會產生大量重試衝突，且無法在單一語句中完成「檢查庫存 + 扣減」。

`NpgsqlCommand` 配合 `ExecuteNonQueryAsync()` 回傳的受影響行數，可以直接判斷是否搶購成功，與 Go 版本的 `tag.RowsAffected() == 0` 邏輯完全對應。

### 為什麼用 Dapper 做讀取？

查詢不涉及併發競爭，Dapper 的輕量映射足夠，且 `DefaultTypeMap.MatchNamesWithUnderscores = true` 可自動處理 DB snake_case 欄位到 C# PascalCase 屬性的映射，無需額外設定。

### 為什麼用 Controller 而非 Minimal API？

Controller 是大多數 .NET 專案的主流寫法，支援 `[ApiController]` 自動 model validation（搭配 `DataAnnotations`），也更易於與 filter、middleware 整合。`CreateOrderRequest` 使用 `[Required]` 和 `[Range]` 讓框架自動回傳 400，GUID 格式驗證則在 Controller 內手動處理。

### JSON 格式相容性

透過 `AddControllers().AddJsonOptions(...)` 全域設定 `JsonNamingPolicy.SnakeCaseLower`，確保 API 回應格式（欄位名稱）與 Go 版本完全一致。

### Port 配置（避免衝突）

| 服務 | Go 版本 | .NET 版本 |
|------|---------|----------|
| API | 8080 | 8081 |
| PostgreSQL | 5432 | 5433 |
| Redis | 6379 | 6380 |

兩個版本可以同時執行並共用同一份 k6 負載測試腳本（修改 `BASE_URL` 即可）。

## Phase 2（未實作）：Redis 預扣庫存

當 TPS 需求超過 10,000，可將第一道庫存檢查移至 Redis（Lua script 保證原子性），非同步寫入 DB。這引入最終一致性的 tradeoff，需要額外的對帳機制。
