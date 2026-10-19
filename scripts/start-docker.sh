#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="$repo_root/src/Compose.yaml"
project_file="$repo_root/src/services/EventCatalog/EventCatalog.csproj"

command -v docker >/dev/null || { echo 'Docker is required. Install and start Docker Desktop.' >&2; exit 1; }
command -v dotnet >/dev/null || { echo '.NET 10 SDK is required.' >&2; exit 1; }
dotnet ef --version

echo 'Starting PostgreSQL and waiting until it is ready...'
docker compose -f "$compose_file" up -d --wait --wait-timeout 120 eventcatalog-db

echo 'Applying EF Core migrations...'
ConnectionStrings__EventCatalog='Host=localhost;Port=5432;Database=eventcatalog_dev;Username=postgres;Password=postgres' \
    dotnet ef database update \
    --context EventCatalog.Data.EventCatalogDbContext \
    --project "$project_file" \
    --startup-project "$project_file"

echo 'Building and starting all services...'
docker compose -f "$compose_file" up -d --build

echo 'Swagger: http://localhost:8080/swagger'
echo 'Admin:   http://localhost:5173'
echo 'Admin-Console:   http://localhost:8180'

