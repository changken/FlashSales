package handler

import (
	"errors"
	"net/http"

	"github.com/changken/flashsales/internal/repository"
	"github.com/changken/flashsales/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
)

type OrderHandler struct {
	svc *service.OrderService
}

func NewOrderHandler(svc *service.OrderService) *OrderHandler {
	return &OrderHandler{svc: svc}
}

type createOrderRequest struct {
	CampaignID     string  `json:"campaign_id" binding:"required" example:"00000000-0000-0000-0000-000000000002"`
	UserID         string  `json:"user_id" binding:"required" example:"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"`
	Qty            int     `json:"qty" binding:"required,min=1" example:"1"`
	IdempotencyKey *string `json:"idempotency_key" example:"unique-uuid-from-client"`
}

type createOrderResponse struct {
	OrderID   string  `json:"order_id" example:"d6aa7191-1ffc-4be3-a224-bf8bf6be5ebb"`
	Status    int16   `json:"status" example:"1"`
	UnitPrice float64 `json:"unit_price" example:"29900"`
	Qty       int     `json:"qty" example:"1"`
	Subtotal  float64 `json:"subtotal" example:"29900"`
	CreatedAt string  `json:"created_at" example:"2024-03-29T10:00:00Z"`
}

type errorResponse struct {
	ErrorCode string `json:"error_code,omitempty" example:"OUT_OF_STOCK"`
	Message   string `json:"message" example:"Sorry, the campaign items are sold out."`
}

// Create godoc
// @Summary      Place a flash sale order
// @Description  Atomically deducts inventory and creates an order. Core concurrency endpoint — protected by DB atomic UPDATE with WHERE guard.
// @Tags         orders
// @Accept       json
// @Produce      json
// @Param        request  body      createOrderRequest   true  "Order request"
// @Success      201      {object}  createOrderResponse
// @Failure      400      {object}  errorResponse  "Invalid request parameters"
// @Failure      409      {object}  errorResponse  "Out of stock or campaign not active"
// @Failure      500      {object}  errorResponse  "Internal server error"
// @Router       /orders [post]
func (h *OrderHandler) Create(c *gin.Context) {
	var req createOrderRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	campaignID, err := uuid.Parse(req.CampaignID)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid campaign_id"})
		return
	}
	userID, err := uuid.Parse(req.UserID)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid user_id"})
		return
	}

	order, err := h.svc.CreateOrder(c.Request.Context(), service.CreateOrderInput{
		CampaignID:     campaignID,
		UserID:         userID,
		Qty:            req.Qty,
		IdempotencyKey: req.IdempotencyKey,
	})
	if err != nil {
		if errors.Is(err, repository.ErrOutOfStock) {
			c.JSON(http.StatusConflict, errorResponse{
				ErrorCode: "OUT_OF_STOCK",
				Message:   "Sorry, the campaign items are sold out or the campaign is not active.",
			})
			return
		}
		c.JSON(http.StatusInternalServerError, errorResponse{Message: "internal server error"})
		return
	}

	c.JSON(http.StatusCreated, createOrderResponse{
		OrderID:   order.ID.String(),
		Status:    int16(order.Status),
		UnitPrice: order.UnitPrice,
		Qty:       order.Qty,
		Subtotal:  order.Subtotal,
		CreatedAt: order.CreatedAt.String(),
	})
}
