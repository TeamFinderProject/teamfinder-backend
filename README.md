# TeamFinder Backend

ASP.NET Core Web API на .NET 10, PostgreSQL 16, Entity Framework Core 10.
Контракт API — [`teamfinder-docs/api-contracts-v1.md`](https://github.com/TeamFinderProject/teamfinder-docs/blob/main/api-contracts-v1.md) и `openapi.yaml`.

> **Состояние этой feature-ветки:** backend-части объединяются. Здесь пока нет модулей `Applications/` и `Teams/`, тестового проекта и нового наполнения БД от второго участника. Таблица ниже описывает целевой состав MVP после объединения. До этого полная сборка и запуск всего приложения не подтверждены.

Версии `Microsoft.EntityFrameworkCore` и `Microsoft.EntityFrameworkCore.Relational` закреплены на `10.0.12` вместе с `Design`: при более старой версии тестовый проект получает ошибку `CS1705`.

После добавления второй backend-части нужно создать миграцию и запустить сборку, тесты и Docker на общей ветке.

## Целевой состав MVP (checkpoint 3)

| Модуль | Endpoint'ы | Где в коде |
|---|---|---|
| Auth: регистрация, вход, текущий пользователь, JWT access (15 мин) + refresh token с ротацией (14 дней, HttpOnly-cookie), не больше 10 попыток входа в минуту | `POST /api/auth/register`, `login`, `refresh`, `logout`, `GET /api/auth/me` | `Auth/` |
| Профиль, навыки, интересующие роли | `GET/PUT /api/profile` | `Profiles/` |
| Справочники | `GET /api/skills`, `GET /api/role-types` | `Dictionaries/` |
| Проекты: **создание сразу с ролями и навыками одним запросом**, каталог с фильтрами, жизненный цикл | `/api/projects/...` | `Projects/ProjectService.cs` |
| Роли: добавить, изменить, закрыть, удалить; matching | `/api/projects/{projectId}/roles/...` | `Projects/` |
| Заявки: подача на роль проекта, отзыв, принятие, отклонение | `/api/projects/{projectId}/roles/{roleId}/applications`, `/api/applications/...` | `Applications/` |
| Команда: состав, выход, исключение | `/api/projects/{projectId}/team`, `DELETE /api/projects/{projectId}/members/{userId}` | `Teams/` |
| Уведомления на сайте | `/api/notifications` | `Notifications/` |
| Telegram-уведомления: привязка по одноразовой ссылке, фоновая отправка | `/api/profile/telegram` | `Notifications/Telegram/` |
| Демо-данные для показа | — | `Data/DemoDataSeeder.cs` |

## Структура

```text
src/TeamFinder.Api/
  Program.cs            DI, JWT, JSON, Swagger, обработка ошибок
  Common/               AppException → {code, message}, блокировка проекта, проверки справочников
  Entities/             сущности EF Core
  Data/                 DbContext, ограничения БД, справочные данные
  Auth/ Profiles/ Dictionaries/ Projects/ Applications/ Teams/ Notifications/
                        модули: Controller (HTTP) → Service (правила, транзакции)
  wwwroot/contract/     копия openapi.yaml из teamfinder-docs — для Swagger UI
tests/TeamFinder.Api.Tests/  интеграционные тесты API на настоящем PostgreSQL
```

Контроллер только принимает HTTP-запрос, берёт id пользователя из токена и вызывает сервис. Все бизнес-правила — в сервисах. Коды ошибок — в `business-rules.md` и разделе 14 контракта.

## Требования

Для разработки:

- .NET SDK 10
- Docker (Docker Desktop на Windows и macOS, Docker Engine с плагином compose на Linux)
- EF Core CLI: `dotnet tool install --global dotnet-ef --version 10.0.12`

Чтобы просто запустить проект, хватит Docker, см. раздел «Запуск всего проекта в Docker».

## Первый запуск после обновления (один раз)

После обновления модели данных старые миграции нужно заменить новой сгенерированной миграцией. Это делает участник, отвечающий за БД. В этой ветке пока остаются старые миграции; они не описывают refresh-токены, уведомления и Telegram.

Следующие команды выполняются после добавления второй backend-части и скрипта `scripts/recreate-initial-migration.sh` в общую ветку:

```bash
./scripts/recreate-initial-migration.sh
git add src/TeamFinder.Api/Migrations
git commit -m "feat: initial migration for schema v2"
```

Скрипт поднимает PostgreSQL, создаёт миграцию `InitialCreate` и пересоздаёт локальную базу. Без папки `Migrations` CI не пройдёт.

## Обычный запуск

```bash
docker compose up -d postgres
dotnet ef database update --project src/TeamFinder.Api --startup-project src/TeamFinder.Api
dotnet run --project src/TeamFinder.Api
```

- `http://localhost:5017/swagger` — Swagger UI. Вверху справа выбор документа:
  - **«Контракт»** — `openapi.yaml` из документации;
  - **«Реализация»** — описание, сгенерированное из кода.
- Кнопка **Authorize** в Swagger (документ «Реализация»): вставить `accessToken` из ответа `/api/auth/login`, после этого защищённые запросы вызываются прямо из Swagger.
- `src/TeamFinder.Api/TeamFinder.Api.http` — готовый сценарий запросов для Rider / VS Code.

## Демо-данные

В режиме разработки (`appsettings.Development.json`) и в Docker при старте создаются демо-пользователи и проекты — если их ещё нет. Пароль у всех `Demo12345!`:

| Email | Кто |
|---|---|
| `ivan@demo.local` | владелец проекта «TeamFinder» (идёт набор) |
| `petr@demo.local` | кандидат, ждёт решения по заявке на Backend |
| `anna@demo.local` | участник команды «TeamFinder» (Frontend) |
| `maria@demo.local` | владелец хакатона и проекта «Бот для расписания» |

Отключить: `Demo:SeedOnStartup = false` (в Docker — `DEMO_SEED=false` в `.env`). Сценарий показа — `teamfinder-docs/demo-guide.md`.

## Telegram-бот

1. В Telegram написать [@BotFather](https://t.me/BotFather) → `/newbot`, придумать имя. Получить токен вида `1234567890:AA...`.
2. Задать настройки (не коммитить токен!):
   - при `dotnet run` — `dotnet user-secrets set "Telegram:BotToken" "<токен>" --project src/TeamFinder.Api` и `dotnet user-secrets set "Telegram:BotUsername" "<имя_бота>" --project src/TeamFinder.Api`;
   - в Docker — `TELEGRAM_BOT_TOKEN` и `TELEGRAM_BOT_USERNAME` в `.env`.
3. Запустить backend. В логах не должно быть «Telegram выключен».
4. На сайте: Профиль → «Подключить Telegram» → открыть ссылку → Start. Бот ответит «Готово».
5. Любое событие (новая заявка, принятие…) придёт в чат через несколько секунд.

Бот читает сообщения через long polling, поэтому работает на локальном компьютере без публичного адреса. Если токен не задан, Telegram просто выключен, остальное работает.

Если в логах `Telegram getUpdates failed: 401` — неверный токен. Если `409` — у бота включён webhook (открыть в браузере `https://api.telegram.org/bot<токен>/deleteWebhook`) или тот же токен запущен ещё где-то. Один токен — один запущенный backend, для разработки каждому лучше завести своего бота.

## Тесты

```bash
docker compose up -d postgres
dotnet test
```

Тесты поднимают API в памяти (`WebApplicationFactory`) и работают с отдельной базой `teamfinder_tests`, которую пересоздают при каждом запуске. Проверяются: регистрация, текущий пользователь, ротация refresh token, привязка Telegram по коду, демо-данные, создание проекта с ролями, валидация ролей, блокировка требований роли, машина состояний заявок, закрытие заявок при принятии и старте проекта, **одновременное принятие на последнее место**, выход и исключение участника.

## Запуск всего проекта в Docker

Нужны только Docker и плагин `docker compose` — .NET и Node.js ставить не надо, всё собирается внутри контейнеров. Рассчитано на требование задания «запуск на Arch Linux с 8 ГБ памяти»: у контейнеров стоят лимиты памяти (база 512 МБ, API 512 МБ, nginx 128 МБ).

```bash
# Arch Linux, один раз:
sudo pacman -S --needed docker docker-compose git
sudo systemctl enable --now docker

# Папки teamfinder-backend и teamfinder-frontend должны лежать рядом
git clone https://github.com/TeamFinderProject/teamfinder-backend.git
git clone https://github.com/TeamFinderProject/teamfinder-frontend.git
cd teamfinder-backend
sudo docker compose up -d --build
```

Чтобы не писать `sudo` каждый раз: `sudo usermod -aG docker $USER` и перелогиниться.

Без `.env` проект поднимается с демо-ключом JWT — для показа на своём компьютере этого достаточно. Для общего сервера: `cp .env.example .env` и задать свой `JWT_SIGNING_KEY` (от 32 символов, например `openssl rand -base64 48`).

- `http://localhost:8081` — сайт (nginx отдаёт frontend и пересылает `/api` в backend);
- `http://localhost:8080/swagger` — API напрямую;
- миграции применяются при старте контейнера `api`.

## Конфигурация

| Ключ | По умолчанию | Назначение |
|---|---|---|
| `ConnectionStrings:TeamFinder` | локальный PostgreSQL | Строка подключения |
| `Jwt:SigningKey` | в `appsettings.Development.json`; в Docker демо-ключ из `docker-compose.yml`, если не задан `JWT_SIGNING_KEY` | Ключ подписи JWT, ≥ 32 символов. Для общего сервера — свой ключ в переменной `Jwt__SigningKey` |
| `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays` | 15 / 14 | Сроки жизни токенов |
| `Jwt:SecureCookie` | `true` (`false` в Development и в Docker без HTTPS) | Флаг `Secure` у refresh-cookie |
| `Database:MigrateOnStartup` | `false` | Применять миграции при старте (включено в Docker) |
| `Jwt:RefreshReuseGraceSeconds` | 30 | Сколько секунд старый refresh token ещё принимается после замены (несколько вкладок) |
| `Jwt:LoginAttemptsPerMinute` | 10 | Лимит попыток входа с одного IP |
| `ReverseProxy:TrustPrivateNetworks` | `false` (`true` в Docker) | API за nginx: брать IP пользователя из `X-Forwarded-For`, если запрос пришёл от прокси из частной сети. Без этого в Docker лимит входа общий на всех |
| `Telegram:BotToken` / `Telegram:BotUsername` | пусто | Токен и имя бота. Пусто — Telegram выключен |
| `Telegram:SiteUrl` | `http://localhost:5173` (в Docker `http://localhost:8081`) | Адрес сайта для ссылки «Открыть» в сообщении |
| `Demo:SeedOnStartup` | `false` (`true` в Development и Docker) | Создать демо-данные при старте |
| `Cors:AllowedOrigins` | `http://localhost:5173`, `http://localhost:8081` | С каких адресов браузеру можно обращаться к API напрямую. Нужно, если frontend ходит в API не через прокси — иначе браузер получает `200`, но блокирует ответ (ошибка CORS, которую видели на checkpoint 2) |

## Как это работает внутри

- **Аутентификация** — `Auth/AuthService.cs`. Refresh token в БД хранится только как SHA-256 хеш. Повторное использование отозванного токена отзывает всю цепочку (защита от кражи), кроме первых 30 секунд после замены — это на случай нескольких вкладок.
- **Создание проекта с ролями** — `ProjectService.CreateAsync`: проект, роли и требования добавляются в один `SaveChanges`, то есть в одну транзакцию.
- **Конкурентный доступ** — `Common/ProjectLock.cs`: каждая операция, меняющая заявки, участников или состояние проекта, открывает транзакцию и блокирует строку проекта `SELECT ... FOR UPDATE`. Операции одного проекта идут строго по очереди, поэтому вместимость роли не превышается.
- **Уведомления** пишутся в таблицу `notifications` в той же транзакции, что и событие (transactional outbox). Фоновая задача `TelegramWorker` отправляет их в Telegram, почта и push подключатся так же в v1.0.
