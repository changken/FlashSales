package repository

import (
	"context"
	"errors"

	"github.com/changken/flashsales/internal/model"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

var ErrOutOfStock = errors.New("out of stock or campaign not active")

type OrderRepo struct {
	db *pgxpool.Pool
}

func NewOrderRepo(db *pgxpool.Pool) *OrderRepo {
	return &OrderRepo{db: db}
}

// GetByIdempotencyKey returns an existing order for the given key, or nil if not found.
func (r *OrderRepo) GetByIdempotencyKey(ctx context.Context, key string) (*model.FlashOrder, error) {
	o := &model.FlashOrder{}
	err := r.db.QueryRow(ctx, `
		SELECT id, campaign_id, user_id, qty, unit_price, subtotal, status, created_at, updated_at
		FROM flash_order
		WHERE idempotency_key = $1
	`, key).Scan(
		&o.ID, &o.CampaignID, &o.UserID, &o.Qty, &o.UnitPrice, &o.Subtotal, &o.Status, &o.CreatedAt, &o.UpdatedAt,
	)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return o, nil
}

// CreateOrder atomically deducts inventory and inserts an order within a transaction.
// Core concurrency logic: single UPDATE with WHERE guard — no SELECT FOR UPDATE needed.
func (r *OrderRepo) CreateOrder(ctx context.Context, campaignID, userID uuid.UUID, qty int, idempotencyKey *string) (*model.FlashOrder, error) {
	tx, err := r.db.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer tx.Rollback(ctx) //nolint:errcheck

	// Step 1: Atomic deduct — only succeeds if campaign is active, in-time, and has stock.
	// RowsAffected == 0 means one of the WHERE conditions failed (sold out or campaign invalid).
	tag, err := tx.Exec(ctx, `
		UPDATE campaign
		SET remaining_qty = remaining_qty - $1,
		    updated_at    = NOW()
		WHERE id            = $2
		  AND status        = 1
		  AND remaining_qty >= $1
		  AND start_at      <= NOW()
		  AND end_at        >= NOW()
		  AND deleted_at    IS NULL
	`, qty, campaignID)
	if err != nil {
		return nil, err
	}
	if tag.RowsAffected() == 0 {
		return nil, ErrOutOfStock
	}

	// Step 2: Fetch the sale_price snapshot from campaign (within same tx for consistency).
	var salePrice float64
	err = tx.QueryRow(ctx, `SELECT sale_price FROM campaign WHERE id = $1`, campaignID).Scan(&salePrice)
	if err != nil {
		return nil, err
	}

	// Step 3: Insert order record with idempotency key.
	orderID := uuid.New()
	subtotal := salePrice * float64(qty)
	_, err = tx.Exec(ctx, `
		INSERT INTO flash_order (id, campaign_id, user_id, qty, unit_price, subtotal, status, idempotency_key, created_at, updated_at)
		VALUES ($1, $2, $3, $4, $5, $6, $7, $8, NOW(), NOW())
	`, orderID, campaignID, userID, qty, salePrice, subtotal, model.OrderConfirmed, idempotencyKey)
	if err != nil {
		return nil, err
	}

	if err := tx.Commit(ctx); err != nil {
		return nil, err
	}

	// Re-fetch created_at from DB for accurate timestamp in response
	var order model.FlashOrder
	err = r.db.QueryRow(ctx, `
		SELECT id, campaign_id, user_id, qty, unit_price, subtotal, status, idempotency_key, created_at, updated_at
		FROM flash_order WHERE id = $1
	`, orderID).Scan(
		&order.ID, &order.CampaignID, &order.UserID, &order.Qty, &order.UnitPrice,
		&order.Subtotal, &order.Status, &order.IdempotencyKey, &order.CreatedAt, &order.UpdatedAt,
	)
	if err != nil {
		return nil, err
	}
	return &order, nil
}
