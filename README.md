# TeamFinder Backend

Backend веб-сервиса TeamFinder.

## Технологии

- C#
- ASP.NET Core
- .NET 10
- OpenAPI
- Swagger UI

## Текущий статус

Реализован первоначальный backend skeleton:

- запускаемый ASP.NET Core Web API;
- endpoint `GET /health`;
- OpenAPI-описание API;
- Swagger UI;
- базовая структура проекта.

Подключение PostgreSQL и Entity Framework Core будет добавлено на следующих этапах разработки.

## Запуск

Из корня репозитория:

```bash
dotnet restore
dotnet build
dotnet run --project src/TeamFinder.Api/TeamFinder.Api.csproj