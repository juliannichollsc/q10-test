#!/usr/bin/env sh
set -eu

API_URL="${API_URL:-http://localhost:8088/api}"
RABBITMQ_URL="${RABBITMQ_URL:-http://localhost:15672}"
RABBITMQ_USER="${RABBITMQ_USER:-orderflow}"
RABBITMQ_PASSWORD="${RABBITMQ_PASSWORD:-orderflow_local_only}"
SKU="${SKU:-ABC-02}"

sql() {
  docker compose exec -T postgres psql -U orderflow -d "$1" -tAc "$2"
}

stock() {
  sql orderflow_inventory "select available from stock where sku = '$SKU'"
}

echo "1. Creating an order for 1 x $SKU"
order_id=$(curl -fsS -X POST "$API_URL/orders" -H "Content-Type: application/json" \
  -d "{\"customerName\":\"Idempotency demo\",\"sku\":\"$SKU\",\"quantity\":1}" |
  sed -E 's/.*"id":"([^"]+)".*/\1/')
echo "   Order $order_id"

printf "2. Waiting for the inventory decision"
for _ in $(seq 1 30); do
  status=$(curl -fsS "$API_URL/orders/$order_id" | sed -E 's/.*"status":"([^"]+)".*/\1/')
  [ "$status" != "Pending" ] && break
  printf "."
  sleep 1
done
echo " $status"

before=$(stock)
event_id=$(sql orderflow_orders "select event_id from outbox where payload->>'orderId' = '$order_id'")
echo "3. Stock for $SKU after the first delivery: $before"

echo "4. Re-publishing the same OrderCreated event ($event_id) twice to RabbitMQ"
body=$(sql orderflow_orders "select json_build_object(
  'properties', json_build_object('delivery_mode', 2, 'message_id', event_id::text, 'content_type', 'application/json'),
  'routing_key', routing_key,
  'payload', payload::text,
  'payload_encoding', 'string')
  from outbox where event_id = '$event_id'")
for attempt in 1 2; do
  printf "   Attempt %s: " "$attempt"
  curl -fsS -u "$RABBITMQ_USER:$RABBITMQ_PASSWORD" -H "Content-Type: application/json" \
    -X POST "$RABBITMQ_URL/api/exchanges/%2F/orderflow.events/publish" -d "$body"
  echo
done

sleep 3
after=$(stock)
inbox=$(sql orderflow_inventory "select count(*) from inbox where event_id = '$event_id'")
results=$(sql orderflow_inventory "select count(*) from outbox where payload->>'orderId' = '$order_id'")
echo "5. Stock for $SKU after the duplicates: $after"
echo "   Inbox rows for the event: $inbox, result events for the order: $results"

if [ "$before" = "$after" ] && [ "$inbox" = "1" ] && [ "$results" = "1" ]; then
  echo "PASS: the duplicate deliveries did not change stock."
else
  echo "FAIL: stock changed from $before to $after."
  exit 1
fi
