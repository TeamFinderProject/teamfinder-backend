# TeamFinder Backend

Backend веб-сервиса TeamFinder: ASP.NET Core Web API на .NET 10, PostgreSQL 16 и Entity Framework Core 10.

## Структура проекта

- `src/TeamFinder.Api/` — API, сущности, `DbContext` и миграции.
- `docker-compose.yml` — локальный PostgreSQL.
- `TeamFinder.slnx` — solution для Rider и .NET CLI.

## Требования

- .NET SDK 10
- Docker Desktop с Docker Compose
- EF Core CLI 10.0.12: `dotnet tool install --global dotnet-ef --version 10.0.12`

## Запуск с нуля

Из корня репозитория:

```bash
docker compose up -d postgres
dotnet restore
dotnet build
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef database update --project src/TeamFinder.Api/TeamFinder.Api.csproj --startup-project src/TeamFinder.Api/TeamFinder.Api.csproj
dotnet run --project src/TeamFinder.Api/TeamFinder.Api.csproj
```

Если EF Core CLI уже установлен, повторно устанавливать его не нужно. Локальная строка подключения в `src/TeamFinder.Api/appsettings.json` предназначена только для разработки. Для других окружений задавайте `ConnectionStrings__TeamFinder` через секреты или переменные среды.

## Проверка

HTTP-профиль Rider использует `http://localhost:5017`.

- `GET http://localhost:5017/health` — ожидается `{"status":"ok"}`.
- `GET http://localhost:5017/health/database` — проверяет подключение к PostgreSQL.
- `http://localhost:5017/swagger` — Swagger UI в среде разработки.

Ожидаемый результат миграции: таблицы для 11 сущностей, `__EFMigrationsHistory`, 18 навыков в `skills` и 8 типов ролей в `role_types`. Для проверки «с нуля» используйте отдельную пустую базу; не удаляйте существующую базу с данными.

## Проверка в CI

GitHub Actions собирает проект на .NET 10, применяет миграции к пустой PostgreSQL 16 и проверяет количество таблиц и записей справочников.

## Решения, ожидающие согласования

1. ERD v1 и состав полей предварительные до командного review.
2. Шкала навыков использует четыре уровня (`Beginner / Intermediate / Advanced / Expert`), но в бизнес-требованиях встречается три уровня.
3. Справочники навыков и типов ролей предложены как начальные значения.
4. Транзакционная проверка вместимости и бизнес-правила обработки заявок ещё не реализованы.
5. Наличие профиля после регистрации пока не обеспечивается сервисным слоем.
6. Пароль в локальной строке подключения не предназначен для общих или производственных окружений.

## Последующие миграции

После согласования модели изменяйте сущности и `TeamFinderDbContext`, затем выполняйте:

```bash
dotnet ef migrations add <MigrationName> --project src/TeamFinder.Api/TeamFinder.Api.csproj --startup-project src/TeamFinder.Api/TeamFinder.Api.csproj
dotnet ef database update --project src/TeamFinder.Api/TeamFinder.Api.csproj --startup-project src/TeamFinder.Api/TeamFinder.Api.csproj
```

Не редактируйте миграции, уже применённые к общим окружениям.
