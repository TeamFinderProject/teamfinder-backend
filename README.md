# TeamFinder Backend — начальная схема БД

**Статус:** предварительная реализация для командной проверки. ERD v1 ещё требует согласования.

## Выбранный способ развёртывания

- СУБД: PostgreSQL 16 через Docker Compose для локальной разработки.
- ORM и миграции: Entity Framework Core 8 с провайдером Npgsql.
- Создание схемы: команда `dotnet ef database update`.
- Справочники `skills` и `role_types` заполняются начальной миграцией; их значения также описаны через `HasData`.

## Требования

- .NET SDK 8
- Docker с Docker Compose
- EF Core CLI: `dotnet tool install --global dotnet-ef --version 8.0.11`

## Развёртывание с нуля

Выполните из корня репозитория:

```bash
docker compose up -d postgres
dotnet restore TeamFinder.Api/TeamFinder.Api.csproj
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef database update --project TeamFinder.Api/TeamFinder.Api.csproj --startup-project TeamFinder.Api/TeamFinder.Api.csproj
dotnet run --project TeamFinder.Api/TeamFinder.Api.csproj
```

Локальная строка подключения хранится в `TeamFinder.Api/appsettings.json` и соответствует Docker Compose. Пароль предназначен только для локальной разработки; для других окружений задайте `ConnectionStrings__TeamFinder` через переменные окружения или секреты.

Проверки работоспособности: `/health` и `/health/database` на порту, который покажет приложение при запуске. Для проверки «с нуля» используйте отдельную пустую базу; не удаляйте существующую базу с данными.

Ожидаемый результат: таблицы для 11 сущностей, служебная таблица `__EFMigrationsHistory`, 18 начальных навыков в `skills` и 8 типов ролей в `role_types`.

## Ограничения и решения для подтверждения

1. ERD v1 пока не утверждена командой; поля и ограничения предварительные.
2. В коде предложены уровни `Beginner / Intermediate / Advanced / Expert`, но бизнес-требования также упоминают трёхуровневую шкалу. Требуется решение владельца продукта.
3. Список навыков и типов ролей — начальный вариант для согласования.
4. Ограничение вместимости роли требует транзакционной проверки при создании `Membership` и защиты от параллельного принятия заявок. Сервисный слой ещё не реализован.
5. Правила подачи и принятия заявок, закрытия других заявок и переходов проекта пока не реализованы. Этот PR создаёт начальную структуру данных, а не готовый API.
6. Наличие профиля после регистрации пока контролируется приложением; схема допускает пользователя без профиля.
7. API на этом этапе содержит только проверки работоспособности, Swagger и подключение к БД.

## Создание следующих миграций

После согласования модели измените сущности и `TeamFinderDbContext`, затем выполните:

```bash
dotnet ef migrations add <MigrationName> --project TeamFinder.Api/TeamFinder.Api.csproj --startup-project TeamFinder.Api/TeamFinder.Api.csproj
dotnet ef database update --project TeamFinder.Api/TeamFinder.Api.csproj --startup-project TeamFinder.Api/TeamFinder.Api.csproj
```

Изменения схемы должны выполняться новыми миграциями. Не редактируйте миграции, уже применённые к общим окружениям.
