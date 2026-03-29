package service

import (
	"context"

	"github.com/changken/flashsales/internal/model"
	"github.com/changken/flashsales/internal/repository"
	"github.com/google/uuid"
)

type ProductService struct {
	repo *repository.ProductRepo
}

func NewProductService(repo *repository.ProductRepo) *ProductService {
	return &ProductService{repo: repo}
}

func (s *ProductService) GetByID(ctx context.Context, id uuid.UUID) (*model.Product, error) {
	return s.repo.GetByID(ctx, id)
}
