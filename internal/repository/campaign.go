package repository

import (
	"context"

	"github.com/changken/flashsales/internal/model"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"
)

type CampaignRepo struct {
	db *pgxpool.Pool
}

func NewCampaignRepo(db *pgxpool.Pool) *CampaignRepo {
	return &CampaignRepo{db: db}
}

func (r *CampaignRepo) List(ctx context.Context) ([]model.CampaignWithProduct, error) {
	rows, err := r.db.Query(ctx, `
		SELECT c.id, c.product_id, c.name, c.sale_price,
		       c.total_qty, c.remaining_qty, c.start_at, c.end_at, c.status,
		       c.created_by, c.updated_by, c.deleted_by,
		       c.created_at, c.updated_at, c.deleted_at,
		       p.name AS product_name, p.price AS orig_price
		FROM campaign c
		JOIN product p ON p.id = c.product_id
		WHERE c.deleted_at IS NULL
		ORDER BY c.created_at DESC
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var campaigns []model.CampaignWithProduct
	for rows.Next() {
		var c model.CampaignWithProduct
		err := rows.Scan(
			&c.ID, &c.ProductID, &c.Name, &c.SalePrice,
			&c.TotalQty, &c.RemainingQty, &c.StartAt, &c.EndAt, &c.Status,
			&c.CreatedBy, &c.UpdatedBy, &c.DeletedBy,
			&c.CreatedAt, &c.UpdatedAt, &c.DeletedAt,
			&c.ProductName, &c.OrigPrice,
		)
		if err != nil {
			return nil, err
		}
		campaigns = append(campaigns, c)
	}
	return campaigns, rows.Err()
}

func (r *CampaignRepo) GetByID(ctx context.Context, id uuid.UUID) (*model.CampaignWithProduct, error) {
	c := &model.CampaignWithProduct{}
	err := r.db.QueryRow(ctx, `
		SELECT c.id, c.product_id, c.name, c.sale_price,
		       c.total_qty, c.remaining_qty, c.start_at, c.end_at, c.status,
		       c.created_by, c.updated_by, c.deleted_by,
		       c.created_at, c.updated_at, c.deleted_at,
		       p.name AS product_name, p.price AS orig_price
		FROM campaign c
		JOIN product p ON p.id = c.product_id
		WHERE c.id = $1 AND c.deleted_at IS NULL
	`, id).Scan(
		&c.ID, &c.ProductID, &c.Name, &c.SalePrice,
		&c.TotalQty, &c.RemainingQty, &c.StartAt, &c.EndAt, &c.Status,
		&c.CreatedBy, &c.UpdatedBy, &c.DeletedBy,
		&c.CreatedAt, &c.UpdatedAt, &c.DeletedAt,
		&c.ProductName, &c.OrigPrice,
	)
	if err != nil {
		return nil, err
	}
	return c, nil
}
