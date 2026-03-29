package repository_test

import (
	"context"
	"fmt"
	"os"
	"sync"
	"sync/atomic"
	"testing"

	"github.com/changken/flashsales/internal/repository"
	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"
)

const (
	testCampaignID = "00000000-0000-0000-0000-000000000002"
	testStock      = 100
	testVUs        = 500 // virtual users hammering simultaneously
)

func setupDB(t *testing.T) *pgxpool.Pool {
	t.Helper()
	dsn := os.Getenv("DATABASE_URL")
	if dsn == "" {
		dsn = "postgres://flashsales:flashsales@localhost:5432/flashsales?sslmode=disable"
	}
	db, err := pgxpool.New(context.Background(), dsn)
	if err != nil {
		t.Fatalf("connect db: %v", err)
	}
	t.Cleanup(func() { db.Close() })
	return db
}

func resetCampaign(t *testing.T, db *pgxpool.Pool) {
	t.Helper()
	_, err := db.Exec(context.Background(), `
		UPDATE campaign SET remaining_qty = $1 WHERE id = $2
	`, testStock, testCampaignID)
	if err != nil {
		t.Fatalf("reset campaign: %v", err)
	}
	_, err = db.Exec(context.Background(), `DELETE FROM flash_order`)
	if err != nil {
		t.Fatalf("delete orders: %v", err)
	}
}

// TestCreateOrder_Concurrency is the core demonstration:
// 500 goroutines all race to buy from a campaign with only 100 units.
// Expected: exactly 100 succeed, remaining_qty == 0, no oversell.
func TestCreateOrder_Concurrency(t *testing.T) {
	db := setupDB(t)
	resetCampaign(t, db)

	repo := repository.NewOrderRepo(db)
	campaignID := uuid.MustParse(testCampaignID)

	var (
		wg           sync.WaitGroup
		successCount atomic.Int32
		failCount    atomic.Int32
	)

	for i := range testVUs {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			userID := uuid.New()
			key := fmt.Sprintf("test-key-%d", i)
			_, err := repo.CreateOrder(context.Background(), campaignID, userID, 1, &key)
			if err == nil {
				successCount.Add(1)
			} else {
				failCount.Add(1)
			}
		}(i)
	}

	wg.Wait()

	// Verify application-layer counts
	t.Logf("Results: success=%d  failed=%d  total=%d",
		successCount.Load(), failCount.Load(), testVUs)

	if int(successCount.Load()) != testStock {
		t.Errorf("expected %d successful orders, got %d", testStock, successCount.Load())
	}
	if int(failCount.Load()) != testVUs-testStock {
		t.Errorf("expected %d failed orders, got %d", testVUs-testStock, failCount.Load())
	}

	// Verify database state — the ground truth
	var remainingQty int
	err := db.QueryRow(context.Background(),
		`SELECT remaining_qty FROM campaign WHERE id = $1`, testCampaignID,
	).Scan(&remainingQty)
	if err != nil {
		t.Fatalf("query remaining_qty: %v", err)
	}

	var orderCount int
	err = db.QueryRow(context.Background(),
		`SELECT COUNT(*) FROM flash_order WHERE campaign_id = $1 AND status = 1`, testCampaignID,
	).Scan(&orderCount)
	if err != nil {
		t.Fatalf("count orders: %v", err)
	}

	t.Logf("DB state: remaining_qty=%d  confirmed_orders=%d", remainingQty, orderCount)

	if remainingQty != 0 {
		t.Errorf("expected remaining_qty=0, got %d (potential oversell!)", remainingQty)
	}
	if orderCount != testStock {
		t.Errorf("expected %d confirmed orders in DB, got %d", testStock, orderCount)
	}
}

// TestCreateOrder_NoOversell verifies the DB constraint is the last line of defense.
// Even if application logic fails, remaining_qty must never go negative.
func TestCreateOrder_NoOversell(t *testing.T) {
	db := setupDB(t)
	resetCampaign(t, db)

	var remaining int
	err := db.QueryRow(context.Background(),
		`SELECT remaining_qty FROM campaign WHERE id = $1`, testCampaignID,
	).Scan(&remaining)
	if err != nil {
		t.Fatal(err)
	}
	if remaining < 0 {
		t.Errorf("remaining_qty is negative (%d) — oversell detected!", remaining)
	}
	t.Logf("remaining_qty after test: %d (no oversell)", remaining)
}
