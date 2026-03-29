package repository

import (
	"context"

	"github.com/changken/flashsales/internal/model"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"
)

type ProductRepo struct {
	db *pgxpool.Pool
}

func NewProductRepo(db *pgxpool.Pool) *ProductRepo {
	return &ProductRepo{db: db}
}

func (r *ProductRepo) GetByID(ctx context.Context, id uuid.UUID) (*model.Product, error) {
	p := &model.Product{}
	err := r.db.QueryRow(ctx, `
		SELECT id, name, description, price, status,
		       created_by, updated_by, deleted_by,
		       created_at, updated_at, deleted_at
		FROM product
		WHERE id = $1 AND deleted_at IS NULL AND status = 0
	`, id).Scan(
		&p.ID, &p.Name, &p.Description, &p.Price, &p.Status,
		&p.CreatedBy, &p.UpdatedBy, &p.DeletedBy,
		&p.CreatedAt, &p.UpdatedAt, &p.DeletedAt,
	)
	if err != nil {
		return nil, err
	}
	return p, nil
}
