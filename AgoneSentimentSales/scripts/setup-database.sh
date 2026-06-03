#!/usr/bin/env bash
# One-command database setup for AG ONE Sentiment Sales
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/Src"

echo "=== AG ONE Sentiment Sales — Database Setup ==="
echo "Connection: see AgoneSentimentSales.API/appsettings.json"
echo ""

if ! command -v dotnet >/dev/null; then
  echo "ERROR: dotnet SDK not found."
  exit 1
fi

export PATH="$PATH:$HOME/.dotnet/tools"
if ! dotnet ef --version >/dev/null 2>&1; then
  echo "Installing dotnet-ef tool..."
  dotnet tool install --global dotnet-ef
fi

echo "Applying EF Core migrations..."
dotnet ef database update \
  --project AgoneSentimentSales.Infrastructure \
  --startup-project AgoneSentimentSales.API

echo ""
echo "Done. Verify with:"
echo "  curl http://localhost:5080/api/health/database   (after starting API)"
echo "Or run scripts/apply-all-schema.sql in SSMS if EF fails."
