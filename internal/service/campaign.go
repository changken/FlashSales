package service

import (
	"context"

	"github.com/changken/flashsales/internal/model"
	"github.com/changken/flashsales/internal/repository"
	"github.com/google/uuid"
)

type CampaignService struct {
	repo *repository.CampaignRepo
}

func NewCampaignService(repo *repository.CampaignRepo) *CampaignService {
	return &CampaignService{repo: repo}
}

func (s *CampaignService) List(ctx context.Context) ([]model.CampaignWithProduct, error) {
	return s.repo.List(ctx)
}

func (s *CampaignService) GetByID(ctx context.Context, id uuid.UUID) (*model.CampaignWithProduct, error) {
	return s.repo.GetByID(ctx, id)
}
