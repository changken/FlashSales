# MVP 功能：限流 + 監控

這個版本在原有的高併發防超賣機制上，新增了生產環境必備的限流和監控功能。

## 新增功能

### 1. 限流（Rate Limiting）

使用 Redis 實作兩層限流保護：

**Per-User 限流**
- 每個 user 每分鐘最多 10 次下單請求
- 防止單一用戶刷單
- 超過限制回傳 `429 Too Many Requests`，`Retry-After: 60` 秒

**Per-Campaign 限流**
- 每個 campaign 每秒最多 100 次請求
- 保護熱門活動不被瞬間流量壓垮
- 超過限制回傳 `429 Too Many Requests`，`Retry-After: 1` 秒

限流配置位於 `Middleware/RateLimitMiddleware.cs`，可根據需求調整：

```csharp
private const int PerUserLimit = 10;      // 調整 per-user 限制
private const int PerCampaignLimit = 100; // 調整 per-campaign 限制
```

### 2. Prometheus Metrics

暴露 `/metrics` endpoint 供 Prometheus 抓取，提供以下指標：

**訂單相關**
- `flashsales_order_requests_total{status}` - 訂單請求總數（success / out_of_stock / error）
- `flashsales_order_duration_seconds` - 訂單處理延遲（histogram，1ms ~ 1s）

**限流相關**
- `flashsales_rate_limit_rejections_total{type}` - 限流拒絕次數（user / campaign）

**HTTP 基礎指標**（由 `prometheus-net` 自動提供）
- `http_requests_received_total` - HTTP 請求總數
- `http_request_duration_seconds` - HTTP 請求延遲

**庫存監控**（預留）
- `flashsales_campaign_remaining_qty{campaign_id}` - 各活動剩餘庫存（需要定期更新）

## 快速啟動

```bash
cd dotnet

# 啟動所有服務（API + PostgreSQL + Redis）
docker compose up -d --build

# 查看 logs
docker compose logs -f app
```

服務端點：
- API: http://localhost:8081
- Swagger: http://localhost:8081/swagger
- Metrics: http://localhost:8081/metrics

## 測試限流

### 測試 Per-User 限流

```bash
# 同一個 user 連續發送 15 次請求（限制是 10 次/分鐘）
for i in {1..15}; do
  curl -X POST http://localhost:8081/api/v1/orders \
    -H "Content-Type: application/json" \
    -d '{
      "campaign_id": "00000000-0000-0000-0000-000000000002",
      "user_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "qty": 1
    }'
  echo ""
done

# 預期：前 10 次成功或庫存不足，後 5 次回傳 429
```

### 測試 Per-Campaign 限流

使用 k6 模擬高併發：

```bash
# 安裝 k6（如果還沒有）
# macOS: brew install k6
# Linux: https://k6.io/docs/getting-started/installation/

# 執行負載測試（200 VUs，持續 10 秒）
k6 run ../k6/load_test.js
```

當 campaign 的請求速率超過 100 req/s 時，會看到 429 錯誤。

## 查看 Metrics

```bash
# 直接查看 metrics endpoint
curl http://localhost:8081/metrics

# 或在瀏覽器開啟
open http://localhost:8081/metrics
```

輸出範例：

```
# HELP flashsales_order_requests_total Total number of order requests
# TYPE flashsales_order_requests_total counter
flashsales_order_requests_total{status="success"} 100
flashsales_order_requests_total{status="out_of_stock"} 400
flashsales_order_requests_total{status="error"} 0

# HELP flashsales_order_duration_seconds Order processing duration in seconds
# TYPE flashsales_order_duration_seconds histogram
flashsales_order_duration_seconds_sum 5.234
flashsales_order_duration_seconds_count 500
flashsales_order_duration_seconds_bucket{le="0.001"} 0
flashsales_order_duration_seconds_bucket{le="0.002"} 12
flashsales_order_duration_seconds_bucket{le="0.004"} 89
...

# HELP flashsales_rate_limit_rejections_total Total number of rate limit rejections
# TYPE flashsales_rate_limit_rejections_total counter
flashsales_rate_limit_rejections_total{type="user"} 5
flashsales_rate_limit_rejections_total{type="campaign"} 23
```

## 整合 Prometheus + Grafana（選配）

建立 `prometheus.yml`：

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'flashsales'
    static_configs:
      - targets: ['localhost:8081']
```

啟動 Prometheus：

```bash
docker run -d -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus
```

啟動 Grafana：

```bash
docker run -d -p 3000:3000 grafana/grafana
```

在 Grafana 中：
1. 新增 Prometheus data source（http://localhost:9090）
2. 建立 dashboard，查詢範例：
   - 訂單成功率：`rate(flashsales_order_requests_total{status="success"}[1m])`
   - P95 延遲：`histogram_quantile(0.95, rate(flashsales_order_duration_seconds_bucket[5m]))`
   - 限流拒絕率：`rate(flashsales_rate_limit_rejections_total[1m])`

## 架構說明

```
HTTP Request
    ↓
RateLimitMiddleware (檢查 Redis)
    ↓ (通過)
OrdersController (記錄 metrics)
    ↓
OrderService
    ↓
OrderRepository (atomic UPDATE)
    ↓
PostgreSQL
```

限流在最外層攔截，避免無效請求消耗 DB 資源。

## 調整限流參數

根據實際負載調整 `RateLimitMiddleware.cs`：

```csharp
// 寬鬆設定（適合測試）
private const int PerUserLimit = 50;
private const int PerCampaignLimit = 500;

// 嚴格設定（適合防刷）
private const int PerUserLimit = 5;
private const int PerCampaignLimit = 50;
```

## 生產環境建議

1. **Redis 持久化**：在 docker-compose.yml 加入 volume
   ```yaml
   redis:
     volumes:
       - redis_data:/data
   ```

2. **Metrics 告警**：設定 Prometheus AlertManager
   - 庫存異常（remaining_qty < 0）
   - 錯誤率飆升（error rate > 5%）
   - 限流頻繁觸發（rejection rate > 10%）

3. **分散式限流**：如果有多個 API instance，Redis 已經是共享狀態，無需額外調整

4. **日誌聚合**：整合 ELK 或 Loki，追蹤被限流的 user_id 和 campaign_id

## 效能影響

- 限流檢查：每次請求增加 ~2ms（2 次 Redis 操作）
- Metrics 記錄：每次請求增加 ~0.1ms（記憶體操作）
- 總體影響：< 3% 延遲增加，換取系統穩定性

## 下一步

- [ ] 加入 Circuit Breaker（當 DB 異常時自動熔斷）
- [ ] 實作 Redis 預扣庫存（Phase 2）
- [ ] 加入分散式追蹤（OpenTelemetry）
- [ ] 實作庫存監控定期更新（background service）
