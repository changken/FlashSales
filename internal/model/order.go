package model

import (
	"time"

	"github.com/google/uuid"
)

type OrderStatus int16

const (
	OrderPending   OrderStatus = 0
	OrderConfirmed OrderStatus = 1
	OrderFailed    OrderStatus = 2
	OrderCancelled OrderStatus = 3
)

type FlashOrder struct {
	ID              uuid.UUID   `json:"id" db:"id"`
	CampaignID      uuid.UUID   `json:"campaign_id" db:"campaign_id"`
	UserID          uuid.UUID   `json:"user_id" db:"user_id"`
	Qty             int         `json:"qty" db:"qty"`
	UnitPrice       float64     `json:"unit_price" db:"unit_price"`
	Subtotal        float64     `json:"subtotal" db:"subtotal"`
	Status          OrderStatus `json:"status" db:"status"`
	IdempotencyKey  *string     `json:"idempotency_key,omitempty" db:"idempotency_key"`
	CreatedBy       *string     `json:"created_by,omitempty" db:"created_by"`
	UpdatedBy       *string     `json:"updated_by,omitempty" db:"updated_by"`
	DeletedBy       *string     `json:"deleted_by,omitempty" db:"deleted_by"`
	CreatedAt       time.Time   `json:"created_at" db:"created_at"`
	UpdatedAt       time.Time   `json:"updated_at" db:"updated_at"`
	DeletedAt       *time.Time  `json:"deleted_at,omitempty" db:"deleted_at"`
}
