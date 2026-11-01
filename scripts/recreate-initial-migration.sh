#!/usr/bin/env bash
# Один раз перед checkpoint 2: пересоздать миграцию по новой модели данных.
# Старые миграции были написаны вручную и удалены. БД есть только у разработчиков, поэтому
# схема создаётся заново. Существующую локальную базу с тестовыми данными этот скрипт удаляет.
set -euo pipefail
cd "$(dirname "$0")/.."

PROJECT=src/TeamFinder.Api

dotnet tool update --global dotnet-ef --version 10.0.12 >/dev/null 2>&1 \
  || dotnet tool install --global dotnet-ef --version 10.0.12

docker compose up -d postgres

dotnet build "$PROJECT"
dotnet ef migrations add InitialCreate --project "$PROJECT" --startup-project "$PROJECT"
dotnet ef database drop --force --project "$PROJECT" --startup-project "$PROJECT"
dotnet ef database update --project "$PROJECT" --startup-project "$PROJECT"

echo
echo "Готово: создана папка $PROJECT/Migrations, база пересоздана."
echo "Закоммитьте папку Migrations: git add $PROJECT/Migrations && git commit -m 'feat: initial migration for schema v2'"
