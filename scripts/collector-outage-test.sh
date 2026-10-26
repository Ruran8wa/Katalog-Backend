#!/usr/bin/env bash
set -euo pipefail

app_url="${APP_URL:-http://localhost:${APP_PORT:-5297}}"
db_password="${SA_PASSWORD:-}"
if [[ -z "$db_password" && -f .env ]]; then
  db_password="$(sed -n 's/^SA_PASSWORD=//p' .env | head -n 1)"
fi
db_password="${db_password:-YourStrong!Passw0rd}"
compose=(docker compose)
email="collector-outage.$(date +%s)@example.com"
password='CollectorTest!2026'

cleanup() {
  "${compose[@]}" start otel-collector >/dev/null 2>&1 || true
}
trap cleanup EXIT

"${compose[@]}" exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$db_password" -C -d "${DB_NAME:-KatalogDb}" -b \
  -Q "SET IDENTITY_INSERT Categories ON;
      IF NOT EXISTS (SELECT 1 FROM Categories WHERE Id = 1)
      INSERT INTO Categories (Id, CategoryName, CategoryParentId) VALUES (1, 'Outage Test', NULL);
      SET IDENTITY_INSERT Categories OFF;
      SET IDENTITY_INSERT Products ON;
      IF NOT EXISTS (SELECT 1 FROM Products WHERE Id = 1)
      INSERT INTO Products (Id, Name, Description, BasePrice, Material, CategoryId)
      VALUES (1, 'Outage Test Product', 'Seeded by the Collector outage test', 100, 'Test', 1);
      SET IDENTITY_INSERT Products OFF;
      SET IDENTITY_INSERT Variants ON;
      IF NOT EXISTS (SELECT 1 FROM Variants WHERE Id = 1)
      INSERT INTO Variants (Id, Name, SKU, Quantity, PriceOverride, ProductId)
      VALUES (1, 'Outage Test Variant', 'OUTAGE-TEST-001', 6, NULL, 1);
      SET IDENTITY_INSERT Variants OFF;"

token="$(curl --fail --silent -X POST "${app_url}/api/auth/register" \
  -H 'Content-Type: application/json' \
  -d "{\"firstName\":\"Collector\",\"lastName\":\"Outage\",\"email\":\"${email}\",\"password\":\"${password}\"}" \
  | jq -r '.token')"
[[ -n "$token" && "$token" != "null" ]]

"${compose[@]}" stop otel-collector >/dev/null
response="$(curl --silent --write-out $'\n%{http_code}' -X POST "${app_url}/api/orders" \
  -H "Authorization: Bearer ${token}" -H 'Content-Type: application/json' \
  -d '{"variantId":1,"quantity":1}')"
status="${response##*$'\n'}"
body="${response%$'\n'*}"
[[ "$status" == "202" ]] || {
  echo "Expected 202 while Collector is stopped, got ${status}: ${body}" >&2
  exit 1
}

order_id="$(printf '%s' "$body" | jq -r '.id')"
for _ in $(seq 1 30); do
  order_status="$("${compose[@]}" exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$db_password" -C -d "${DB_NAME:-KatalogDb}" -h -1 -W \
    -Q "SELECT Status FROM Orders WHERE Id = ${order_id};" | tr -d '[:space:]')"
  if [[ "$order_status" == "2" || "$order_status" == "3" ]]; then
    echo "Collector outage test passed; order ${order_id} reached final status ${order_status}."
    exit 0
  fi
  sleep 2
done

echo "Order ${order_id} was not processed while Collector was stopped." >&2
exit 1
