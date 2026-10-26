#!/usr/bin/env bash
set -euo pipefail

compose=(docker compose)
app_url="${APP_URL:-http://localhost:${APP_PORT:-5297}}"
db_password="${SA_PASSWORD:-}"
if [[ -z "$db_password" && -f .env ]]; then
  db_password="$(sed -n 's/^SA_PASSWORD=//p' .env | head -n 1)"
fi
db_password="${db_password:-YourStrong!Passw0rd}"

cleanup() {
  "${compose[@]}" down --volumes --remove-orphans
}
trap cleanup EXIT

"${compose[@]}" up -d --build

for attempt in $(seq 1 60); do
  if curl --fail --silent "${app_url}/health/ready" >/dev/null; then
    break
  fi
  if [[ "$attempt" == 60 ]]; then
    echo "The API did not become ready." >&2
    exit 1
  fi
  sleep 2
done

"${compose[@]}" exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
-S localhost -U sa -P "$db_password" -C -d "${DB_NAME:-KatalogDb}" -b \
  -Q "SET IDENTITY_INSERT Categories ON;
      IF NOT EXISTS (SELECT 1 FROM Categories WHERE Id = 1)
      INSERT INTO Categories (Id, CategoryName, CategoryParentId) VALUES (1, 'Smoke Test', NULL);
      SET IDENTITY_INSERT Categories OFF;
      SET IDENTITY_INSERT Products ON;
      IF NOT EXISTS (SELECT 1 FROM Products WHERE Id = 1)
      INSERT INTO Products (Id, Name, Description, BasePrice, Material, CategoryId)
      VALUES (1, 'Smoke Test Product', 'Seeded by the container smoke test', 100, 'Test', 1);
      SET IDENTITY_INSERT Products OFF;
      SET IDENTITY_INSERT Variants ON;
      IF NOT EXISTS (SELECT 1 FROM Variants WHERE Id = 1)
      INSERT INTO Variants (Id, Name, SKU, Quantity, PriceOverride, ProductId)
      VALUES (1, 'Smoke Test Variant', 'SMOKE-TEST-001', 6, NULL, 1);
      SET IDENTITY_INSERT Variants OFF;"

status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "${app_url}/api/products/1")"
[[ "$status" == "200" ]] || {
  echo "Expected GET /api/products/1 to return 200, got ${status}." >&2
  exit 1
}

"${compose[@]}" stop sqlserver
status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "${app_url}/health/ready")"
[[ "$status" == "503" ]] || {
  echo "Expected readiness to return 503 with SQL Server stopped, got ${status}." >&2
  exit 1
}

status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "${app_url}/health/live")"
[[ "$status" == "200" ]] || {
  echo "Expected liveness to return 200 with SQL Server stopped, got ${status}." >&2
  exit 1
}

echo "Container smoke test passed."
