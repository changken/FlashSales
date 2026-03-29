package service

import (
	"context"
	"errors"

	"github.com/changken/flashsales/internal/model"
	"github.com/changken/flashsales/internal/repository"
	"github.com/google/uuid"
)

type OrderService struct {
	repo *repository.OrderRepo
}

func NewOrderService(repo *repository.OrderRepo) *OrderService {
	return &OrderService{repo: repo}
}

type CreateOrderInput struct {
	CampaignID     uuid.UUID
	UserID         uuid.UUID
	Qty            int
	IdempotencyKey *string
}

func (s *OrderService) CreateOrder(ctx context.Context, in CreateOrderInput) (*model.FlashOrder, error) {
	// Idempotency check: return existing order if key was already used.
	if in.IdempotencyKey != nil && *in.IdempotencyKey != "" {
		existing, err := s.repo.GetByIdempotencyKey(ctx, *in.IdempotencyKey)
		if err != nil {
			return nil, err
		}
		if existing != nil {
			return existing, nil
		}
	}

	order, err := s.repo.CreateOrder(ctx, in.CampaignID, in.UserID, in.Qty, in.IdempotencyKey)
	if err != nil {
		if errors.Is(err, repository.ErrOutOfStock) {
			return nil, err
		}
		return nil, err
	}
	return order, nil
}
