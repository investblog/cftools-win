# CLAUDE.md

Инструкция для работы с проектом CFTools for Windows.

## Что это

Windows-порт браузерного расширения [cloudflare-tools](W:\Projects\cloudflare-tools) — десктопное приложение для bulk-операций с Cloudflare зонами.

**Стек:** C# / .NET 8 / WinUI 3 (Windows App SDK 1.6) / MSIX
**Спецификация:** `SPEC.md` — **читай перед любой работой**

## Оригинальное расширение

Исходники: `W:\Projects\cloudflare-tools`

Файлы-источники для портирования:

| Исходник (TypeScript) | Порт (C#) | Что делает |
|----------------------|-----------|-----------|
| `src/background/cf-client.ts` | `Services/CloudflareApi.cs` | HTTP-клиент CF API |
| `src/background/queue.ts` | `Services/RequestPool.cs` | Rate-limited очередь с backoff |
| `src/shared/domains/parser.ts` | `Services/DomainParser.cs` | Парсер доменов из текста |
| `src/shared/types/api.ts` | `Models/CloudflareModels.cs` | DTO: CfUser, CfZone, CfAccount |
| `src/shared/types/errors.ts` | `Models/ErrorModels.cs` | Нормализация ошибок CF API |
| `src/shared/types/tasks.ts` | `Models/CloudflareModels.cs` | BatchInfo, TaskEntry |
| `src/entrypoints/background.ts` | ViewModels | Batch processing логика |

**Чего НЕ портируем:**
- `vault.ts` → заменяем на Windows Credential Manager
- `ledger.ts` → не нужен в MVP (in-memory)
- `messaging/protocol.ts` → не нужен (прямые вызовы, нет изоляции SW)
- `cf-dashboard.content.ts` → нет аналога на десктопе

## Структура проекта

```
cftools-win/
├── CFTools.sln
├── src/CFTools/
│   ├── CFTools.csproj
│   ├── App.xaml                    # Точка входа, тема
│   ├── MainWindow.xaml             # NavigationView + страницы
│   ├── Views/
│   │   ├── AuthPage.xaml           # Email + API Key
│   │   ├── AddDomainsPage.xaml     # Bulk add
│   │   └── PurgeCachePage.xaml     # Bulk purge
│   ├── ViewModels/                 # CommunityToolkit.Mvvm
│   │   ├── AuthViewModel.cs
│   │   ├── AddDomainsViewModel.cs
│   │   └── PurgeCacheViewModel.cs
│   ├── Services/
│   │   ├── CloudflareApi.cs        # ← cf-client.ts
│   │   ├── CredentialStore.cs      # Windows Credential Manager
│   │   ├── RequestPool.cs          # ← queue.ts
│   │   └── DomainParser.cs         # ← parser.ts
│   └── Models/
│       ├── CloudflareModels.cs     # ← api.ts + tasks.ts
│       └── ErrorModels.cs          # ← errors.ts
├── tests/CFTools.Tests/            # xUnit
│   ├── DomainParserTests.cs
│   ├── ErrorNormalizerTests.cs
│   └── RequestPoolTests.cs
├── .github/workflows/build.yml
├── SPEC.md
└── CLAUDE.md
```

## Команды разработки

```bash
# Сборка
dotnet build src/CFTools/CFTools.csproj
dotnet run --project src/CFTools/CFTools.csproj

# Тесты
dotnet test tests/CFTools.Tests/CFTools.Tests.csproj

# Публикация
dotnet publish src/CFTools/CFTools.csproj -c Release
```

Основная разработка — Visual Studio 2022. Отладка — F5.

## Cloudflare API

Base URL: `https://api.cloudflare.com/client/v4`

Headers:
```
X-Auth-Email: {email}
X-Auth-Key: {apiKey}
Content-Type: application/json
```

Endpoints в MVP:
```
GET  /user                         → верификация
GET  /accounts                     → список аккаунтов
GET  /zones?account.id=X&page=P    → список зон
GET  /zones?name=domain.com        → preflight-проверка
POST /zones                        → создание зоны
POST /zones/{id}/purge_cache       → очистка кэша
```

JSON: snake_case. Используем `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }`.

## Ключевые паттерны

### Rate limiting (из queue.ts)

- `SemaphoreSlim` для concurrency (max 4, cap 8)
- Exponential backoff: `min(20s, 500ms × 2^attempt) + jitter(30%)`
- Retry-After header → берём значение из CF ответа
- Retry только если `NormalizedError.Retryable == true`

### Error normalization (из errors.ts)

CF коды → категории:
- **Auth** (no retry): 10000, 10001, 6003, 6100-6103, 9103, 9106
- **RateLimit** (retry): 429
- **Validation** (skip): 1061 (exists), 1003 (invalid name)
- **Dependency** (blocked): 1099 (has subscription)
- **Network** (retry): 5xx, timeout

### Domain parser (из parser.ts)

1. ASCII regex: извлечь домены из текста
2. Unicode → Punycode: `System.Globalization.IdnMapping`
3. Фильтр: только root-домены (не субдомены)
4. Special SLDs: co.uk, com.br и т.д. считаются root
5. Дедупликация + валидация TLD

### Batch processing (из background.ts)

Batch — **ephemeral** (in-memory). Теряется при закрытии приложения или смене страницы.

```
foreach task in batch:
    if cancelled → break
    pool.Add((ct) => cfClient.Operation(task, ct))
    update progress (processed/total, ETA)
    notify UI via ObservableProperty
```

ETA = `avg_latency × remaining_count`

Все методы принимают `CancellationToken`. Cancel → token отменяется.
Pause → новые задачи не стартуют, текущие завершаются.
Смена страницы во время batch → confirm dialog → cancel.

### MVVM

CommunityToolkit.Mvvm (source generators):
```csharp
[ObservableProperty] string _email;
[RelayCommand] async Task ConnectAsync() { ... }
```

XAML: `{x:Bind ViewModel.Email, Mode=TwoWay}`

## Зависимости

| Пакет | Версия | Зачем |
|-------|--------|-------|
| Microsoft.WindowsAppSDK | 1.6.x | WinUI 3 |
| CommunityToolkit.Mvvm | 8.x | MVVM source gen |

Всё остальное (`HttpClient`, `System.Text.Json`, `IdnMapping`, `SemaphoreSlim`) — встроено в .NET 8.

## Функции MVP

1. **Auth** — Email + API Key → Credential Manager → verify → accounts
2. **Bulk Add** — textarea → parse → preflight → create zones с прогрессом
3. **Bulk Purge** — список зон → multi-select → purge_cache с прогрессом

## Out of Scope (MVP)

Не делаем: API Token auth, batch persistence, Bulk Delete, export, DNS ops, settings bulk, i18n, telemetry, auto-update, tray.

## Очередь после MVP

P1: Bulk Delete, Zone List + CSV Export, batch result export
P2: API Token auth, DNS Import/Export, file logging
P3: Bulk SSL Mode, Security Level, Always HTTPS

## Logging

- `ILogger` (Microsoft.Extensions.Logging), output: Debug console
- **НИКОГДА не логировать:** API Key, auth headers
- Можно: email, domains, CF error codes, latency, batch progress

## Окружение разработки

- Win11, Visual Studio 2022
- Git + GitHub
- Hyper-V VM (Win10/Win11) — тестирование совместимости
- GitHub Actions — CI/CD сборка MSIX
