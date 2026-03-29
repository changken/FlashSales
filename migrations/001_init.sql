-- ============================================================
-- Flash Sale MVP — PostgreSQL Migration
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ------------------------------------------------------------
-- PRODUCT
-- status: 0=active, 1=inactive
-- ------------------------------------------------------------
CREATE TABLE product (
    id          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    name        VARCHAR(255) NOT NULL,
    description TEXT,
    price       NUMERIC(12, 2) NOT NULL CHECK (price >= 0),
    status      SMALLINT    NOT NULL DEFAULT 0,
    created_by  VARCHAR(100),
    updated_by  VARCHAR(100),
    deleted_by  VARCHAR(100),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at  TIMESTAMPTZ
);

-- ------------------------------------------------------------
-- CAMPAIGN
-- status: 0=draft, 1=active, 2=ended, 3=cancelled
-- ------------------------------------------------------------
CREATE TABLE campaign (
    id            UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    product_id    UUID        NOT NULL REFERENCES product(id),
    name          VARCHAR(255) NOT NULL,
    sale_price    NUMERIC(12, 2) NOT NULL CHECK (sale_price >= 0),
    total_qty     INT         NOT NULL CHECK (total_qty > 0),
    remaining_qty INT         NOT NULL CHECK (remaining_qty >= 0),
    start_at      TIMESTAMPTZ NOT NULL,
    end_at        TIMESTAMPTZ NOT NULL,
    status        SMALLINT    NOT NULL DEFAULT 0,
    created_by    VARCHAR(100),
    updated_by    VARCHAR(100),
    deleted_by    VARCHAR(100),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at    TIMESTAMPTZ,
    CONSTRAINT chk_campaign_dates      CHECK (end_at > start_at),
    CONSTRAINT chk_remaining_lte_total CHECK (remaining_qty <= total_qty)
);

-- ------------------------------------------------------------
-- ORDER
-- status: 0=pending, 1=confirmed, 2=failed, 3=cancelled
-- Note: "order" is a reserved word in PostgreSQL, use flash_order
-- ------------------------------------------------------------
CREATE TABLE flash_order (
    id               UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    campaign_id      UUID        NOT NULL REFERENCES campaign(id),
    user_id          UUID        NOT NULL,   -- no FK, MVP mock
    qty              INT         NOT NULL DEFAULT 1 CHECK (qty > 0),
    unit_price       NUMERIC(12, 2) NOT NULL,  -- price snapshot
    subtotal         NUMERIC(12, 2) NOT NULL,  -- qty * unit_price
    status           SMALLINT    NOT NULL DEFAULT 0,
    idempotency_key  VARCHAR(128) UNIQUE,       -- prevent duplicate orders
    created_by       VARCHAR(100),
    updated_by       VARCHAR(100),
    deleted_by       VARCHAR(100),
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at       TIMESTAMPTZ
);

-- ------------------------------------------------------------
-- INDEXES
-- ------------------------------------------------------------
-- Campaign lookup by product + status
CREATE INDEX idx_campaign_product_status ON campaign(product_id, status)
    WHERE deleted_at IS NULL;

-- Campaign 時間區間查詢
CREATE INDEX idx_campaign_time ON campaign(start_at, end_at)
    WHERE deleted_at IS NULL;

-- Order lookup by user
CREATE INDEX idx_order_user ON flash_order(user_id)
    WHERE deleted_at IS NULL;

-- Order lookup by campaign
CREATE INDEX idx_order_campaign ON flash_order(campaign_id)
    WHERE deleted_at IS NULL;

-- ------------------------------------------------------------
-- SEED DATA (for local dev / k6 testing)
-- ------------------------------------------------------------
-- Insert a product
INSERT INTO product (id, name, description, price, status, created_by)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'iPhone 15 Pro',
    '256GB, 黑色鈦金屬',
    35900.00,
    0,
    'seed'
);

-- Insert a campaign (active, starts now, ends 24 hours later, 100 units)
INSERT INTO campaign (id, product_id, name, sale_price, total_qty, remaining_qty, start_at, end_at, status, created_by)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    '雙11限量搶購',
    29900.00,
    100,
    100,
    NOW() - INTERVAL '1 minute',
    NOW() + INTERVAL '24 hours',
    1,
    'seed'
);
