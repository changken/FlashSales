package model

import (
	"time"

	"github.com/google/uuid"
)

type ProductStatus int16

const (
	ProductActive   ProductStatus = 0
	ProductInactive ProductStatus = 1
)

type Product struct {
	ID          uuid.UUID     `json:"id" db:"id"`
	Name        string        `json:"name" db:"name"`
	Description *string       `json:"description,omitempty" db:"description"`
	Price       float64       `json:"price" db:"price"`
	Status      ProductStatus `json:"status" db:"status"`
	CreatedBy   *string       `json:"created_by,omitempty" db:"created_by"`
	UpdatedBy   *string       `json:"updated_by,omitempty" db:"updated_by"`
	DeletedBy   *string       `json:"deleted_by,omitempty" db:"deleted_by"`
	CreatedAt   time.Time     `json:"created_at" db:"created_at"`
	UpdatedAt   time.Time     `json:"updated_at" db:"updated_at"`
	DeletedAt   *time.Time    `json:"deleted_at,omitempty" db:"deleted_at"`
}
