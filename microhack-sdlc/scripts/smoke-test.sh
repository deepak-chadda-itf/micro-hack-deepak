#!/usr/bin/env bash
# Post-deploy smoke test: exercises the real cart -> checkout flow over HTTPS.
# Usage: scripts/smoke-test.sh <frontend-url> <api-url>
# Requires: curl, jq. Uses the demo payment provider (skips payment if not demo).
set -euo pipefail

FRONTEND_URL="${1:?frontend url required}"
API_URL="${2:?api url required}"
FRONTEND_URL="${FRONTEND_URL%/}"
API_URL="${API_URL%/}"

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "ok  - $*"; }

wait_for() {
  local url="$1" attempts=40
  for ((i = 1; i <= attempts; i++)); do
    if [[ "$(curl -s -o /dev/null -w '%{http_code}' "$url")" == "200" ]]; then return 0; fi
    sleep 10
  done
  return 1
}

wait_for "$FRONTEND_URL/" || fail "frontend not reachable at $FRONTEND_URL"
pass "frontend reachable"

runtime_config="$(curl -fsS "$FRONTEND_URL/runtime-config.js")"
[[ "$runtime_config" == *"$API_URL"* || "$runtime_config" == *"${API_URL#https://}"* ]] \
  || fail "runtime-config.js does not point at the API: $runtime_config"
pass "frontend is configured with the API URL"

wait_for "$API_URL/" || fail "api not reachable at $API_URL"
pass "api reachable"

product_id="$(curl -fsS "$API_URL/api/products" | jq -er '.[0].productId')" || fail "no products returned (seed data missing?)"
branch_id="$(curl -fsS "$API_URL/api/branches" | jq -er '.[0].branchId')" || fail "no branches returned"
pass "seeded data present (product $product_id, branch $branch_id)"

cors_header="$(curl -s -o /dev/null -D - -H "Origin: $FRONTEND_URL" "$API_URL/api/products" | tr -d '\r' | grep -i '^access-control-allow-origin:' || true)"
[[ "$cors_header" == *"$FRONTEND_URL"* ]] || fail "CORS does not allow the frontend origin ($cors_header)"
pass "CORS allows the frontend origin"

cart_id="$(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen 2>/dev/null || python3 -c 'import uuid; print(uuid.uuid4())' 2>/dev/null || python -c 'import uuid; print(uuid.uuid4())')"
cart="$(curl -fsS -X POST "$API_URL/api/cart/$cart_id/items" \
  -H 'Content-Type: application/json' -d "{\"productId\":$product_id,\"quantity\":2}")"
[[ "$(jq -r '.itemCount' <<<"$cart")" == "2" ]] || fail "cart did not contain 2 items: $cart"
pass "item added to cart"

provider="$(curl -fsS "$API_URL/api/checkout/config" | jq -r '.provider')"
if [[ "$provider" != "demo" ]]; then
  echo "payment provider is '$provider'; skipping checkout"
  echo "Smoke test passed."
  exit 0
fi

checkout() {
  curl -s -o /tmp/checkout.json -w '%{http_code}' -X POST "$API_URL/api/checkout/$cart_id" \
    -H 'Content-Type: application/json' \
    -d "{\"branchId\":$branch_id,\"customerName\":\"Smoke Test\",\"paymentMethodId\":\"$1\"}"
}

[[ "$(checkout demo-decline)" == "402" ]] || fail "declined payment should return 402: $(cat /tmp/checkout.json)"
[[ "$(curl -fsS "$API_URL/api/cart/$cart_id" | jq -r '.itemCount')" == "2" ]] || fail "declined payment must leave the cart intact"
pass "declined payment returns 402 and keeps the cart"

[[ "$(checkout demo-success)" == "201" ]] || fail "approved payment should return 201: $(cat /tmp/checkout.json)"
order_id="$(jq -er '.orderId' /tmp/checkout.json)"
[[ "$(curl -fsS "$API_URL/api/orders/$order_id" | jq -r '.status')" == "confirmed" ]] || fail "order $order_id not confirmed"
[[ "$(curl -fsS "$API_URL/api/cart/$cart_id" | jq -r '.itemCount')" == "0" ]] || fail "cart should be empty after checkout"
pass "approved payment created confirmed order $order_id and cleared the cart"

echo "Smoke test passed."
