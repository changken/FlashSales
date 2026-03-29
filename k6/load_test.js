import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

// Custom counters to track outcomes
const successOrders = new Counter('success_orders');
const failedOrders  = new Counter('failed_orders');

export const options = {
  vus: 500,          // 500 virtual users, all hitting at once
  duration: '30s',
  thresholds: {
    // We expect exactly 100 successes (matching campaign stock)
    // The test itself won't enforce this number, but the summary will show it.
    'success_orders': [],
    http_req_failed: ['rate<0.01'],   // <1% connection errors (not 409s)
    http_req_duration: ['p(95)<2000'],
  },
};

// Campaign ID from seed data in 001_init.sql
const CAMPAIGN_ID = '00000000-0000-0000-0000-000000000002';
const BASE_URL    = __ENV.BASE_URL || 'http://localhost:8080';

export default function () {
  const userId = generateUUID();

  const payload = JSON.stringify({
    campaign_id:     CAMPAIGN_ID,
    user_id:         userId,
    qty:             1,
    idempotency_key: generateUUID(),
  });

  const res = http.post(`${BASE_URL}/api/v1/orders`, payload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (res.status === 201) {
    successOrders.add(1);
    check(res, { 'order created': (r) => r.status === 201 });
  } else if (res.status === 409) {
    failedOrders.add(1);
    // 409 is expected when stock runs out — not a test failure
  } else {
    // Unexpected status — mark as failed
    check(res, { 'unexpected error': () => false });
  }

  sleep(0.1);
}

// Simple UUID v4 generator for k6 (no external dependencies)
function generateUUID() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
