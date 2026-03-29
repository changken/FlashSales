package model

import (
	"time"

	"github.com/google/uuid"
)

type CampaignStatus int16

const (
	CampaignDraft     CampaignStatus = 0
	CampaignActive    CampaignStatus = 1
	CampaignEnded     CampaignStatus = 2
	CampaignCancelled CampaignStatus = 3
)

type Campaign struct {
	ID           uuid.UUID      `json:"id" db:"id"`
	ProductID    uuid.UUID      `json:"product_id" db:"product_id"`
	Name         string         `json:"name" db:"name"`
	SalePrice    float64        `json:"sale_price" db:"sale_price"`
	TotalQty     int            `json:"total_qty" db:"total_qty"`
	RemainingQty int            `json:"remaining_qty" db:"remaining_qty"`
	StartAt      time.Time      `json:"start_at" db:"start_at"`
	EndAt        time.Time      `json:"end_at" db:"end_at"`
	Status       CampaignStatus `json:"status" db:"status"`
	CreatedBy    *string        `json:"created_by,omitempty" db:"created_by"`
	UpdatedBy    *string        `json:"updated_by,omitempty" db:"updated_by"`
	DeletedBy    *string        `json:"deleted_by,omitempty" db:"deleted_by"`
	CreatedAt    time.Time      `json:"created_at" db:"created_at"`
	UpdatedAt    time.Time      `json:"updated_at" db:"updated_at"`
	DeletedAt    *time.Time     `json:"deleted_at,omitempty" db:"deleted_at"`
}

// CampaignWithProduct embeds product info for API responses
type CampaignWithProduct struct {
	Campaign
	ProductName string  `json:"product_name" db:"product_name"`
	OrigPrice   float64 `json:"orig_price" db:"orig_price"`
}
