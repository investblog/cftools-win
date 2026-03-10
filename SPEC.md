# CFTools for Windows — Technical Specification

## Overview

Десктопное приложение для bulk-операций с Cloudflare зонами. Логика заимствована из браузерного расширения [cloudflare-tools](https://github.com/investblog/cloudflare-tools) и адаптирована под Windows/.NET runtime.

| | |
|---|---|
| **Тип** | Windows Desktop App |
| **Платформа** | Windows 10 1809+ / Windows 11 |
| **Стек** | C# / .NET 8 / WinUI 3 (Windows App SDK 1.6) |
| **Пакет** | MSIX (GitHub Actions CI/CD) |
| **Лицензия** | MIT |

## Отличия от расширения

| Аспект | Расширение | Windows-приложение |
|--------|-----------|-------------------|
| Среда | Browser Service Worker | .NET 8 desktop process |
| API-вызовы | `fetch()` через SW (обход CORS) | `HttpClient` (CORS не актуален) |
| Хранение ключей | AES-256-GCM в `chrome.storage` | Windows Credential Manager |
| UI | Vanilla DOM + Side Panel | WinUI 3 / XAML + MVVM |
| Коммуникация | Message passing (panel↔background) | Прямые вызовы сервисов |
| Персистентность | IndexedDB (Ledger) | In-memory (эфемерный batch) |

## Архитектура

```
┌─────────────────────────────────────────┐
│              WinUI 3 App                │
├─────────────────────────────────────────┤
│  Views (XAML)     │  ViewModels (MVVM)  │
│  ├─ AuthPage      │  ├─ AuthViewModel   │
│  ├─ AddPage       │  ├─ AddViewModel    │
│  └─ PurgePage     │  └─ PurgeViewModel  │
├─────────────────────────────────────────┤
│              Services                   │
│  ├─ CloudflareApi   (HTTP client)       │
│  ├─ CredentialStore (Win Credential Mgr)│
│  ├─ RequestPool     (rate limiting)     │
│  └─ DomainParser    (text → domains)    │
├─────────────────────────────────────────┤
│              Models                     │
│  ├─ CfZone, CfAccount, CfUser          │
│  ├─ ApiResponse<T>, ApiError            │
│  ├─ BatchState, TaskEntry               │
│  └─ NormalizedError, ErrorCategory      │
└─────────────────────────────────────────┘
                    │
                    ▼
        Cloudflare API v4 (direct)
```

## Структура проекта

```
cftools-win/
├── CFTools.sln
├── src/
│   └── CFTools/
│       ├── CFTools.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / .cs
│       ├── Views/
│       │   ├── AuthPage.xaml / .cs
│       │   ├── AddDomainsPage.xaml / .cs
│       │   └── PurgeCachePage.xaml / .cs
│       ├── ViewModels/
│       │   ├── AuthViewModel.cs
│       │   ├── AddDomainsViewModel.cs
│       │   └── PurgeCacheViewModel.cs
│       ├── Services/
│       │   ├── CloudflareApi.cs
│       │   ├── CredentialStore.cs
│       │   ├── RequestPool.cs
│       │   └── DomainParser.cs
│       ├── Models/
│       │   ├── CloudflareModels.cs
│       │   └── ErrorModels.cs
│       ├── Assets/
│       │   └── cftools.ico
│       └── Package.appxmanifest
├── tests/
│   └── CFTools.Tests/
│       ├── CFTools.Tests.csproj
│       ├── DomainParserTests.cs
│       ├── ErrorNormalizerTests.cs
│       └── RequestPoolTests.cs
├── .github/
│   └── workflows/
│       └── build.yml
├── SPEC.md
├── CLAUDE.md
└── README.md
```

## Функции MVP

### 1. Авторизация

Ввод Email + Global API Key. Верификация через API. Хранение в Windows Credential Manager.

**Почему Global API Key, а не API Token в MVP:**
- Расширение использует Global API Key — паритет поведения
- Один набор credentials на все операции, проще UX
- API Token требует отдельную форму для выбора scopes/permissions — усложняет MVP

**Принимаемые риски:** Global API Key даёт полный доступ к аккаунту. Пользователь должен понимать это (предупреждение в UI).

**Migration path к API Token (P2):**
- `CloudflareApi` уже принимает headers через абстракцию — достаточно добавить второй тип auth
- Заголовок `Authorization: Bearer {token}` вместо `X-Auth-Email` + `X-Auth-Key`
- UI: переключатель "Global Key / API Token" на странице авторизации

**Flow:**
1. Пользователь вводит Email + API Key
2. Приложение вызывает `GET /user` для проверки
3. При успехе — получает список аккаунтов (`GET /accounts`)
4. Сохраняет credentials в Windows Credential Manager (target: `CFTools::{email}`)
5. При следующем запуске — автоматически загружает из Credential Manager

**UI состояния:**
| Состояние | Что видит пользователь |
|-----------|----------------------|
| Initial | Пустые поля Email + API Key, кнопка "Connect" |
| Connecting | Поля disabled, спиннер на кнопке |
| Connected | Имя пользователя, аккаунт, кнопка "Disconnect" |
| Error | Сообщение об ошибке, поля доступны для правки |
| Saved credentials | Автозаполнение при старте, автоподключение |

**Ошибки авторизации:**
| Ситуация | UI |
|----------|-----|
| Невалидный key/email | "Invalid credentials. Check your email and API key." |
| Сетевая ошибка | "Cannot reach Cloudflare API. Check your connection." |
| Аккаунт suspended | "Account is suspended." |

### 2. Bulk Add Domains

Массовое добавление доменов в Cloudflare. Парсер доменов, preflight-проверка, создание зон с прогрессом.

**Flow:**
1. Пользователь вставляет текст с доменами в TextBox
2. `DomainParser.Parse()` извлекает домены
3. Preflight: для каждого домена `GET /zones?name={domain}` через `RequestPool`
4. Показать результат: will-create / exists / invalid / duplicate
5. Пользователь нажимает "Create"
6. Для каждого `will-create` домена: `POST /zones` через `RequestPool`
7. Прогресс в реальном времени: ProgressBar + счётчики + ETA

**Параметры создания:**
- Account: ComboBox (из авторизации)
- Type: `full` | `partial` (RadioButtons, default: full)
- Jump start: CheckBox (default: true)

**UI состояния:**
| Состояние | Что видит пользователь |
|-----------|----------------------|
| Initial | Пустой TextBox, кнопка "Check" disabled |
| Has input | TextBox с текстом, "Check" enabled |
| Checking | ProgressBar indeterminate, "Checking domains..." |
| Preflight done | ListView: домены с статусами, кнопка "Create All" |
| Creating | ProgressBar determinate, счётчики, Pause/Cancel |
| Paused | ProgressBar замер, кнопка "Resume" |
| Completed | Итоговые счётчики, "Retry Failed" если есть ошибки |

**Блокировки во время batch:**
- TextBox — disabled (нельзя менять input)
- Account ComboBox — disabled
- Навигация — доступна (batch продолжается в фоне? **Нет** — batch отменяется при смене страницы, с confirm dialog)

### 3. Bulk Purge Cache

Массовая очистка кэша для выбранных зон.

**Flow:**
1. Загрузить список зон аккаунта: `GET /zones?account.id={id}` (все страницы)
2. Показать в ListView с CheckBox + поиск
3. Пользователь выбирает зоны
4. **Confirmation dialog:** "Purge cache for {N} zones? This cannot be undone."
5. Для каждой: `POST /zones/{id}/purge_cache` через `RequestPool`
6. Прогресс + результаты

**UI состояния:**
| Состояние | Что видит пользователь |
|-----------|----------------------|
| Initial | Пустой список, загрузка зон |
| Loading zones | ProgressBar indeterminate |
| Zones loaded | ListView с зонами, CheckBox, поиск |
| Zones selected | Кнопка "Purge {N} zones" active |
| Confirm | ContentDialog: "Purge cache for {N} zones?" |
| Purging | ProgressBar, счётчики, Pause/Cancel |
| Completed | Итоги: success/failed |

**Guardrails:**
- Confirmation dialog перед purge (всегда)
- Показать число выбранных зон на кнопке: "Purge 42 zones"
- При выборе >50 зон — дополнительное предупреждение в confirm dialog

## Out of Scope (MVP)

Явно исключено из MVP — не реализуем:

- API Token auth (P2)
- Batch persistence / resume after crash
- Bulk Delete Zones (P1, следующий этап)
- Zone List / Export CSV (P1, следующий этап)
- DNS operations
- Zone settings (SSL, Security Level)
- Localization / i18n
- Telemetry / analytics
- Auto-update mechanism
- System tray / background operation
- Import domains from file (drag & drop)

## Модели данных

### CloudflareModels.cs — API DTO

```csharp
// CF API response wrapper
record ApiResponse<T>(
    bool Success,
    List<ApiError> Errors,
    T Result,
    PaginationInfo? ResultInfo
);

record ApiError(int Code, string Message, List<ApiError>? ErrorChain);

record PaginationInfo(int Page, int PerPage, int Count, int TotalCount, int TotalPages);

// Domain objects
record CfUser(string Id, string Email, string Username, bool Suspended);
record CfAccount(string Id, string Name);

record CfZone(
    string Id, string Name, string Status, string Type,
    string[] NameServers, CfAccount Account, CfPlan Plan
);

record CfPlan(string Id, string Name, string LegacyId);

// Request objects
record CreateZoneRequest(string Name, AccountRef Account, string Type, bool JumpStart);
record AccountRef(string Id);
record PurgeCacheRequest(bool PurgeEverything);
```

### State Models — Orchestration

```csharp
// Операции
enum OperationKind { Create, Delete, Purge }

// Preflight
enum PreflightStatus { WillCreate, Exists, Invalid, Duplicate }

record PreflightEntry(string Domain, PreflightStatus Status, string? ExistingZoneId = null);

// Tasks
enum TaskStatus { Queued, Running, Success, Failed, Skipped, Blocked }

record TaskEntry(
    string Id,
    string Domain,             // домен (create) или zoneId (delete/purge)
    string? ZoneName,          // display name для delete/purge
    OperationKind Operation,
    TaskStatus Status,
    int Attempt,
    int? ErrorCode,
    string? ErrorMessage,
    long? LatencyMs
);

// Batch
enum BatchStatus { Pending, Running, Paused, Completed, Cancelled }

record BatchState(
    OperationKind Operation,
    string AccountId,
    BatchStatus Status,
    int TotalCount,
    int ProcessedCount,
    int SuccessCount,
    int FailedCount,
    int SkippedCount,
    long? EtaMs               // avg_latency × remaining
);
```

### ErrorModels.cs

```csharp
enum ErrorCategory { Auth, RateLimit, Validation, Dependency, Network, Unknown }

record NormalizedError(
    ErrorCategory Category,
    int Code,
    string Message,
    string Recommendation,
    bool Retryable,
    int? RetryAfterMs
);
```

## Cloudflare API

### Base URL
```
https://api.cloudflare.com/client/v4
```

### Auth Headers
```
X-Auth-Email: {email}
X-Auth-Key: {apiKey}
Content-Type: application/json
```

### Endpoints

#### `GET /user` — Верификация credentials
- **Success:** `{ result: CfUser }`
- **Errors:** auth codes (10000, 10001, 6003, 6100-6103, 9103, 9106)
- **Retryable:** нет (кроме 5xx/timeout)

#### `GET /accounts` — Список аккаунтов
- **Success:** `{ result: CfAccount[] }`
- **Pagination:** обычно 1 страница (мало аккаунтов у пользователя)

#### `GET /zones` — Список зон
- **Query params:** `account.id`, `name`, `page`, `per_page` (max 50)
- **Pagination:** итерировать `page` от 1 до `total_pages`
- **Для preflight:** `?name={domain}&per_page=1` — достаточно проверить count > 0
- **Success:** `{ result: CfZone[], result_info: PaginationInfo }`

#### `POST /zones` — Создание зоны
- **Body:** `CreateZoneRequest`
- **Success:** `{ result: CfZone }` (новая зона)
- **Errors:**
  - 1061 → zone already exists (Validation, skip)
  - 1003 → invalid zone name (Validation, fail)
  - Auth codes → credentials problem
- **Retryable:** только 429, 5xx, timeout

#### `POST /zones/{id}/purge_cache` — Очистка кэша
- **Body:** `{ "purge_everything": true }`
- **Success:** `{ result: { id: string } }`
- **Errors:** auth codes, 5xx
- **Retryable:** только 429, 5xx, timeout

### Error Codes

| Код | Категория | Retryable | Описание |
|-----|-----------|-----------|----------|
| 10000, 10001 | Auth | Нет | Invalid credentials |
| 6003, 6100-6103, 9103, 9106 | Auth | Нет | Auth header errors |
| 429 | RateLimit | Да | Too Many Requests (Retry-After header) |
| 1061 | Validation | Нет | Zone already exists |
| 1003 | Validation | Нет | Invalid zone name |
| 1099 | Dependency | Нет | Zone has active subscription |
| 5xx | Network | Да | Server errors |
| timeout | Network | Да | Request timed out |

## Ключевые сервисы

### CloudflareApi.cs

```
class CloudflareApi
├── VerifyCredentials(CancellationToken) → CfUser
├── GetAccounts(CancellationToken) → List<CfAccount>
├── ListZones(accountId, page, perPage, CancellationToken) → PaginatedResult<CfZone>
├── ListAllZones(accountId, CancellationToken) → List<CfZone>
├── CheckZoneExists(domain, CancellationToken) → (bool exists, string? zoneId)
├── CreateZone(domain, accountId, type, jumpStart, CancellationToken) → CfZone
└── PurgeCacheEverything(zoneId, CancellationToken) → string
```

- Все методы принимают `CancellationToken`
- Единый `HttpClient` (reusable, thread-safe)
- Timeout: 30 секунд (через `HttpClient.Timeout` + `CancellationToken`)
- JSON: `System.Text.Json` с `JsonNamingPolicy.SnakeCaseLower`
- `NormalizeError()` — маппинг CF error codes → `NormalizedError`
- `CfApiException` — выбрасывается при API ошибках, содержит `NormalizedError`

### CredentialStore.cs

```
class CredentialStore
├── Save(email, apiKey)
├── Load() → (email, apiKey)?
├── Delete()
└── Exists() → bool
```

- `Windows.Security.Credentials.PasswordVault` (WinRT API, доступен в packaged apps)
- Resource: `"CFTools"`
- Username: email
- Password: apiKey

### RequestPool.cs

```
class RequestPool
├── Add<T>(Func<CancellationToken, Task<T>>, CancellationToken) → Task<T>
├── Pause()
├── Resume()
├── Cancel()
├── GetStats() → PoolStats
└── UpdateConcurrency(int maxConcurrency)
```

**Параметры:**

| Параметр | Значение | Лимит |
|----------|---------|-------|
| Max concurrency | 4 | 8 |
| Max retries | 3 | 5 |
| Base delay | 500ms | — |
| Max delay | 20 000ms | — |
| Jitter | 30% of base delay | — |

**Семантика операций:**

| Операция | Новые задачи | Текущие запросы | Очередь |
|----------|-------------|----------------|---------|
| **Pause** | Не стартуют | Завершаются | Сохраняется |
| **Resume** | Стартуют | — | Продолжает обработку |
| **Cancel** | Не стартуют | `CancellationToken` отменяется | Очищается, tasks получают `TaskCanceledException` |

**Retry policy:**
- Retry на: `429` (rate limit), `5xx` (server error), timeout, network disconnect
- Не retry на: auth errors, validation errors, dependency errors
- Backoff: `min(maxDelay, baseDelay × 2^attempt) + random(0, baseDelay × jitterFactor)`
- Retry-After header: если есть — используем вместо backoff

**Реализация:**
- `SemaphoreSlim` для ограничения concurrency
- `CancellationTokenSource` для отмены (linked с внешним token)
- `TaskCompletionSource<T>` для каждой задачи в очереди

**Счётчики (PoolStats):**

```csharp
record PoolStats(int Pending, int Running, int Completed, int Failed, bool Paused);
```

- `Pending` — в очереди, ждёт слота
- `Running` — выполняется прямо сейчас
- `Completed` — успешно завершено (включая skip)
- `Failed` — ошибка после всех retry

### DomainParser.cs

```
static class DomainParser
├── Parse(text, rootOnly = true) → ParseResult
└── Count(text) → int
```

**Тип парсера:** best-effort extraction. Не претендует на строгий registrable domain parsing (PSL). Окончательная валидация — на стороне Cloudflare API.

**Поддерживаемый вход:**
- Домены по одному на строке: `example.com`
- Домены внутри текста: `"добавить example.com и test.org"`
- URL: `https://example.com/path` → `example.com`
- Mixed content: любой текст с доменами внутри

**Обработка:**
1. ASCII regex: извлечь паттерны `[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+`
2. Unicode → Punycode: `System.Globalization.IdnMapping`
3. Trailing dot removal: `example.com.` → `example.com`
4. Root-domain filter: `www.example.com` → отбрасывается (если rootOnly)
5. Special SLDs: `example.co.uk` — считается root (heuristic список: co, com, net, org, edu, gov, ac, me)
6. Дедупликация (case-insensitive)
7. TLD валидация: минимум 1 буква (отсекает IP-адреса)

**Ограничения (known, accepted):**
- Special SLDs — hardcoded список, не PSL. Может неверно обработать редкие ccTLD
- Не парсит email-адреса (user@example.com → не извлекает домен)

```csharp
record ParseResult(List<string> Domains, List<string> Duplicates, List<string> Invalid);
```

## Batch Lifecycle

### Ephemeral Model

Batch-состояние хранится **только in-memory**. При закрытии приложения или переходе на другую страницу — batch теряется.

**Что это значит для пользователя:**
- Закрыл приложение во время batch → незавершённые задачи потеряны, нужно запустить заново
- Сменил страницу → confirm dialog "Batch in progress. Cancel and leave?"
- Перезапуск ОС → то же, что закрытие

**Retry Failed:**
- Работает только пока приложение открыто и batch на экране
- Повторяет все задачи со статусом `Failed` (retryable и non-retryable)
- Создаёт новый batch из failed items

**Результаты batch:**
- Видны на экране пока страница открыта
- Не сохраняются между сессиями
- Не экспортируются в MVP (export — P1, следующий этап)

### Partial Success

Batch может завершиться со смешанным результатом. Итоговый экран показывает:
```
Completed: 95 success, 3 failed, 2 skipped
[Retry 3 Failed]  [Copy Results]
```

"Copy Results" — копирует текстовый список результатов в буфер обмена (домен + статус + ошибка).

## UI / UX

### Навигация

`NavigationView` (WinUI 3) с пунктами:
- **Add Domains** — bulk-создание зон
- **Purge Cache** — bulk-очистка кэша

Footer: статус авторизации + кнопка Connect/Disconnect.

### Общие UI-паттерны

**ProgressBar + статус:**
```
[████████░░░░░░░░] 45/100 — 12 success, 2 failed, 1 skipped — ETA 0:42
```

**ETA:** `среднее_время_на_задачу × оставшееся_количество`

**Batch controls:**
- Pause → приостановить очередь, текущие запросы завершаются
- Cancel → confirm dialog → отменить всё
- Retry Failed → повторить failed задачи

**Confirmation dialogs:**
- Purge cache → всегда
- Cancel batch → всегда
- Disconnect → если batch в процессе

### Тема

Fluent Design, системная тема (Light/Dark). `RequestedTheme="Default"`.

### Размер окна

- Минимум: 600×500
- По умолчанию: 800×600
- Resizable

## MVVM

`CommunityToolkit.Mvvm` (source-generated):

```csharp
[ObservableProperty] string _email;
[RelayCommand] async Task ConnectAsync();
```

XAML: `{x:Bind ViewModel.Email, Mode=TwoWay}`

## Зависимости

| Пакет | Назначение |
|-------|-----------|
| `Microsoft.WindowsAppSDK` 1.6 | WinUI 3 |
| `CommunityToolkit.Mvvm` | MVVM source generators |

`HttpClient`, `System.Text.Json`, `IdnMapping`, `SemaphoreSlim` — встроены в .NET 8.

## Non-Functional Requirements

### Производительность

| Параметр | Значение |
|----------|---------|
| Ожидаемый размер batch | до 500 доменов |
| Max параллельных запросов | 8 (cap) |
| UI responsiveness | UI thread не блокируется (async/await) |
| Память | batch 500 items < 50 MB |

### Timeout Policy

| Операция | Timeout |
|----------|---------|
| HTTP request к CF API | 30 сек |
| Preflight batch (500 доменов) | до 5 мин |
| Create/Purge batch (500 доменов) | до 15 мин |

Нет hard limit на длительность batch — пользователь может отменить в любой момент.

### Logging

- Framework: `Microsoft.Extensions.Logging` (ILogger)
- Output: Debug console (MVP). Файловый лог — после MVP
- Levels: Information (batch start/complete), Warning (retry), Error (failed tasks)

**Redaction — НИКОГДА не логировать:**
- API Key (ни целиком, ни частично)
- Auth headers
- Полные HTTP request/response bodies с credentials

**Можно логировать:**
- Email (не секрет)
- Domain names
- Error codes и messages от CF API
- Timing / latency
- Batch progress (processed/total)

### Network

- Offline → ошибки при первом запросе, пользователь видит "Cannot reach Cloudflare API"
- Network flap mid-batch → retry policy обрабатывает (exponential backoff)
- Proxy → системный proxy из `HttpClient.DefaultProxy`

## Testing Strategy

### Unit Tests (xUnit)

Приоритетные компоненты для покрытия:

**DomainParser:**
- Извлечение из чистого списка
- Извлечение из mixed text / URLs
- IDN / Punycode
- Дедупликация
- Root domain filtering
- Edge cases: trailing dot, IP address, empty input

**ErrorNormalizer:**
- Все known CF error codes → правильная категория
- Unknown codes → Unknown category
- Retry-After header parsing
- 5xx → Network + retryable

**RequestPool:**
- Concurrency limit (не более N одновременных)
- Retry на retryable errors
- No retry на non-retryable
- Pause / Resume / Cancel semantics
- Backoff timing (approximate)

### Integration Tests

Не в MVP. После MVP — mock HTTP handler для `CloudflareApi`.

### Manual Testing Checklist (MVP)

- [ ] Auth: connect, disconnect, saved credentials restore
- [ ] Add: paste domains, preflight, create, progress, pause, cancel
- [ ] Add: retry failed
- [ ] Purge: load zones, search, select, confirm, purge, progress
- [ ] Edge: large batch (100+ domains), network error mid-batch
- [ ] Edge: disconnect during batch
- [ ] Win10 compatibility (Hyper-V VM)

## CI/CD (GitHub Actions)

```yaml
trigger: push to main, PR
steps:
  1. checkout
  2. setup .NET 8
  3. restore + build (Release)
  4. run tests (xUnit)
  5. publish MSIX
  6. upload artifact
  7. on tag (v*) → create GitHub Release + attach MSIX
```

Детали signing, package identity, runner config — определяются при настройке pipeline.

## Очередь разработки (после MVP)

| Приоритет | Функция | API endpoints |
|-----------|---------|---------------|
| P1 | Bulk Delete Zones | `DELETE /zones/{id}` |
| P1 | Zone List + Export CSV | `GET /zones` |
| P1 | Batch result export | — |
| P2 | API Token auth | `Authorization: Bearer` |
| P2 | Bulk DNS Import | `POST /zones/{id}/dns_records` |
| P2 | DNS Export | `GET /zones/{id}/dns_records/export` |
| P2 | File logging | — |
| P3 | Bulk SSL Mode | `PATCH /zones/{id}/settings/ssl` |
| P3 | Bulk Security Level | `PATCH /zones/{id}/settings/security_level` |
| P3 | Bulk Always HTTPS | `PATCH /zones/{id}/settings/always_use_https` |
