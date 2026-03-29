// Package main is the entry point for the Flash Sale API server.
//
// @title           Flash Sale API
// @version         1.0
// @description     High-concurrency flash sale inventory system. Core design: atomic DB UPDATE prevents overselling under concurrent load.
// @description
// @description     ## Concurrency Strategy
// @description     POST /orders uses a single atomic SQL UPDATE with WHERE guard:
// @description     `UPDATE campaign SET remaining_qty = remaining_qty - $qty WHERE ... AND remaining_qty >= $qty`
// @description     RowsAffected == 0 signals sold-out. No SELECT FOR UPDATE needed.
//
// @contact.name    changken
// @license.name    MIT
//
// @host            localhost:8080
// @BasePath        /api/v1
package main

import (
	"context"
	"fmt"
	"log"
	"os"

	_ "github.com/changken/flashsales/docs"
	"github.com/changken/flashsales/internal/handler"
	"github.com/changken/flashsales/internal/repository"
	"github.com/changken/flashsales/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/joho/godotenv"
	swaggerfiles "github.com/swaggo/files"
	ginSwagger "github.com/swaggo/gin-swagger"
)

func main() {
	_ = godotenv.Load()

	dsn := os.Getenv("DATABASE_URL")
	if dsn == "" {
		log.Fatal("DATABASE_URL is required")
	}

	db, err := pgxpool.New(context.Background(), dsn)
	if err != nil {
		log.Fatalf("unable to connect to database: %v", err)
	}
	defer db.Close()

	if err := db.Ping(context.Background()); err != nil {
		log.Fatalf("database ping failed: %v", err)
	}
	log.Println("database connected")

	// Wire up layers
	productRepo := repository.NewProductRepo(db)
	campaignRepo := repository.NewCampaignRepo(db)
	orderRepo := repository.NewOrderRepo(db)

	productSvc := service.NewProductService(productRepo)
	campaignSvc := service.NewCampaignService(campaignRepo)
	orderSvc := service.NewOrderService(orderRepo)

	productH := handler.NewProductHandler(productSvc)
	campaignH := handler.NewCampaignHandler(campaignSvc)
	orderH := handler.NewOrderHandler(orderSvc)

	r := gin.Default()

	// Swagger UI at /swagger/index.html
	r.GET("/swagger/*any", ginSwagger.WrapHandler(swaggerfiles.Handler))

	v1 := r.Group("/api/v1")
	{
		v1.GET("/products/:id", productH.GetByID)

		v1.GET("/campaigns", campaignH.List)
		v1.GET("/campaigns/:id", campaignH.GetByID)

		v1.POST("/orders", orderH.Create)
	}

	port := os.Getenv("PORT")
	if port == "" {
		port = "8080"
	}
	log.Printf("server starting on :%s  swagger: http://localhost:%s/swagger/index.html", port, port)
	if err := r.Run(fmt.Sprintf(":%s", port)); err != nil {
		log.Fatal(err)
	}
}
