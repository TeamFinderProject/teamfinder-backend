# TeamFinder Backend

Backend веб-сервиса TeamFinder: ASP.NET Core Web API на .NET 10, PostgreSQL 16 и Entity Framework Core 10.

## Структура проекта

- `src/TeamFinder.Api/` — API, сущности, `DbContext` и миграции.
- `docker-compose.yml` — локальный PostgreSQL.
- `TeamFinder.slnx` — решение для Rider и .NET CLI.

## Требования

- .NET SDK 10
- Docker Desktop с Docker Compose
- EF Core CLI: `dotnet tool install --global dotnet-ef --version 10.0.12`

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

Если EF Core CLI уже установлен, повторно устанавливать его не нужно. Строка подключения в `src/TeamFinder.Api/appsettings.json` предназначена только для локальной разработки. Для других окружений задавайте `ConnectionStrings__TeamFinder` через секреты или переменные среды.

## Проверка

HTTP-профиль Rider использует `http://localhost:5017`.

- `GET http://localhost:5017/health` — проверка API; ожидается `{"status":"ok"}`.
- `GET http://localhost:5017/health/database` — проверка подключения к PostgreSQL.
- `http://localhost:5017/swagger` — Swagger UI в среде разработки.

Начальная миграция должна создать 11 таблиц и служебную таблицу `__EFMigrationsHistory`, а также добавить 18 навыков в `skills` и 8 типов ролей в `role_types`. Для проверки «с нуля» используйте отдельную пустую базу; не удаляйте существующую базу с данными.

## Начальная модель данных

Добавлены 11 сущностей TeamFinder v1: `User`, `Profile`, `Skill`, `RoleType`, `UserSkill`, `UserRoleInterest`, `Project`, `ProjectRole`, `RoleSkillRequirement`, `Application` и `Membership`.

Изменения схемы выполняются через EF Core Migrations, а не ручным изменением существующих окружений. После изменения модели создавайте новую миграцию:

```bash
dotnet ef migrations add <MigrationName> --project src/TeamFinder.Api/TeamFinder.Api.csproj --startup-project src/TeamFinder.Api/TeamFinder.Api.csproj
dotnet ef database update --project src/TeamFinder.Api/TeamFinder.Api.csproj --startup-project src/TeamFinder.Api/TeamFinder.Api.csproj
```

## Проверки и ограничения

GitHub Actions собирает проект, применяет начальную миграцию к пустой PostgreSQL 16 и проверяет количество таблиц и записей справочников.

ERD и состав полей остаются предварительными до командного согласования. В коде предложены четыре уровня навыков (`Beginner / Intermediate / Advanced / Expert`), хотя в документации требований встречается трёхуровневая шкала. Транзакционная проверка вместимости ролей, бизнес-операции принятия заявок и закрытия других заявок ещё не реализованы.

Локальный пароль из конфигурации не предназначен для общих или производственных окружений.
