package handler

import (
	"net/http"

	"github.com/changken/flashsales/internal/model"
	"github.com/changken/flashsales/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
)

type CampaignHandler struct {
	svc *service.CampaignService
}

func NewCampaignHandler(svc *service.CampaignService) *CampaignHandler {
	return &CampaignHandler{svc: svc}
}

// List godoc
// @Summary      List all campaigns
// @Description  Returns all flash sale campaigns with remaining inventory
// @Tags         campaigns
// @Produce      json
// @Success      200  {array}   model.CampaignWithProduct
// @Failure      500  {object}  map[string]string
// @Router       /campaigns [get]
func (h *CampaignHandler) List(c *gin.Context) {
	campaigns, err := h.svc.List(c.Request.Context())
	if err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "failed to list campaigns"})
		return
	}
	if campaigns == nil {
		campaigns = []model.CampaignWithProduct{}
	}
	c.JSON(http.StatusOK, campaigns)
}

// GetByID godoc
// @Summary      Get campaign by ID
// @Description  Returns a single campaign with product info and remaining inventory
// @Tags         campaigns
// @Produce      json
// @Param        id   path      string  true  "Campaign UUID"
// @Success      200  {object}  model.CampaignWithProduct
// @Failure      400  {object}  map[string]string
// @Failure      404  {object}  map[string]string
// @Router       /campaigns/{id} [get]
func (h *CampaignHandler) GetByID(c *gin.Context) {
	id, err := uuid.Parse(c.Param("id"))
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid campaign id"})
		return
	}

	campaign, err := h.svc.GetByID(c.Request.Context(), id)
	if err != nil {
		c.JSON(http.StatusNotFound, gin.H{"error": "campaign not found"})
		return
	}

	c.JSON(http.StatusOK, campaign)
}
