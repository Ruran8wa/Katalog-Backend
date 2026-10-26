#!/usr/bin/env bash
set -euo pipefail

app_url="${APP_URL:-http://localhost:${APP_PORT:-5297}}"
db_password="${SA_PASSWORD:-}"
if [[ -z "$db_password" && -f .env ]]; then
  db_password="$(sed -n 's/^SA_PASSWORD=//p' .env | head -n 1)"
fi
db_password="${db_password:-YourStrong!Passw0rd}"
compose=(docker compose)
email="load-test.$(date +%s)@example.com"
password='LoadTest!2026'

"${compose[@]}" up -d --build
trap 'printf "Load test data remains in the local named volume for Grafana review.\\n"' EXIT

for attempt in $(seq 1 60); do
  if curl --fail --silent "${app_url}/health/ready" >/dev/null; then break; fi
  [[ "$attempt" == 60 ]] && { echo "The API did not become ready." >&2; exit 1; }
  sleep 2
done

"${compose[@]}" exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$db_password" -C -d "${DB_NAME:-KatalogDb}" -b \
  -Q "SET IDENTITY_INSERT Categories ON;
      IF NOT EXISTS (SELECT 1 FROM Categories WHERE Id = 1)
      INSERT INTO Categories (Id, CategoryName, CategoryParentId) VALUES (1, 'Load Test', NULL);
      SET IDENTITY_INSERT Categories OFF;
      SET IDENTITY_INSERT Products ON;
      IF NOT EXISTS (SELECT 1 FROM Products WHERE Id = 1)
      INSERT INTO Products (Id, Name, Description, BasePrice, Material, CategoryId)
      VALUES (1, 'Load Test Product', 'Seeded by the observed load test', 100, 'Test', 1);
      SET IDENTITY_INSERT Products OFF;
      SET IDENTITY_INSERT Variants ON;
      IF NOT EXISTS (SELECT 1 FROM Variants WHERE Id = 1)
      INSERT INTO Variants (Id, Name, SKU, Quantity, PriceOverride, ProductId)
      VALUES (1, 'Load Test Variant', 'LOAD-TEST-001', 6, NULL, 1);
      SET IDENTITY_INSERT Variants OFF;"

"${compose[@]}" exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$db_password" -C -d "${DB_NAME:-KatalogDb}" -b \
  -Q "UPDATE Variants SET Quantity = 6 WHERE Id = 1;"

curl --fail --silent -X POST "${app_url}/api/auth/register" \
  -H 'Content-Type: application/json' \
  -d "{\"firstName\":\"Load\",\"lastName\":\"Tester\",\"email\":\"${email}\",\"password\":\"${password}\"}" >/dev/null
token="$(curl --fail --silent -X POST "${app_url}/api/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"${email}\",\"password\":\"${password}\"}" | jq -r '.token')"
[[ -n "$token" && "$token" != "null" ]]

for _ in $(seq 1 20); do
  curl --fail --silent -X POST "${app_url}/api/orders" \
    -H "Authorization: Bearer ${token}" -H 'Content-Type: application/json' \
    -d '{"variantId":1,"quantity":1}' >/dev/null
done

echo "Submitted 20 orders with six units of stock. Wait for the dashboard to show 6 confirmed and 14 rejected orders."
