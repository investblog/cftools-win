# CLAUDE.md

Инструкция для работы с проектом CFTools for Windows.

## Что это

Windows-порт браузерного расширения [cloudflare-tools](W:\Projects\cloudflare-tools) — десктопное приложение для bulk-операций с Cloudflare зонами.

**Стек:** C# / .NET 8 / WinUI 3 (Windows App SDK 1.6) / CommunityToolkit.Mvvm
**Спецификация:** `SPEC.md` — **читай перед любой работой**

## Структура проекта

```
cftools-win/
├── CFTools.sln
├── .editorconfig                     # Code style rules
├── Directory.Build.props             # EnforceCodeStyleInBuild + AnalysisLevel
├── .config/dotnet-tools.json         # CSharpier (local tool)
├── src/CFTools/                      # WinUI 3 app (net8.0-windows10.0.19041.0)
│   ├── CFTools.csproj
│   ├── App.xaml/.cs                  # Entry point, shared services, converters
│   ├── MainWindow.xaml/.cs           # NavigationView + pages
│   ├── Converters/BoolConverters.cs  # Bool↔Visibility
│   ├── Views/
│   │   ├── AuthPage.xaml/.cs
│   │   ├── AddDomainsPage.xaml/.cs
│   │   ├── PurgeCachePage.xaml/.cs
│   │   └── DeleteDomainsPage.xaml/.cs
│   ├── ViewModels/
│   │   ├── AuthViewModel.cs
│   │   ├── AddDomainsViewModel.cs
│   │   ├── PurgeCacheViewModel.cs
│   │   ├── DeleteDomainsViewModel.cs
│   │   └── ZoneSelection.cs          # Shared zone wrapper for Purge/Delete
│   ├── Services/
│   │   ├── CloudflareApi.cs          # CF API v4 client
│   │   ├── CredentialStore.cs        # Windows Credential Manager
│   │   ├── RequestPool.cs            # Rate-limited queue with backoff
│   │   └── DomainParser.cs           # Domain extraction from text
│   └── Models/
│       ├── CloudflareModels.cs       # API DTOs + state models
│       └── ErrorModels.cs            # Error normalization
├── src/CFTools.Core/                 # Pure .NET 8 library (no WinUI)
│   └── CFTools.Core.csproj           # Links Models/ + Services/ for testing
├── tests/CFTools.Tests/              # xUnit (targets net8.0 via Core)
│   ├── DomainParserTests.cs          # 14 tests
│   ├── ErrorNormalizerTests.cs       # 12 tests
│   └── RequestPoolTests.cs           # 8 tests
└── SPEC.md
```

## Команды разработки

```bash
# Сборка (WinUI 3 requires VS2022 MSBuild)
"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
  src/CFTools/CFTools.csproj -p:Platform=x64 -p:Configuration=Debug

# Тесты (работают через dotnet CLI — тестируют CFTools.Core, не WinUI)
dotnet test tests/CFTools.Tests/CFTools.Tests.csproj

# Запуск
src/CFTools/bin/x64/Debug/net8.0-windows10.0.19041.0/CFTools.exe

# Форматирование (CSharpier)
dotnet csharpier format src/ tests/

# Проверка форматирования (CI)
dotnet csharpier check src/ tests/
```

**Важно:** `dotnet build` для WinUI не работает — нужен VS2022 MSBuild из-за XAML tooling.
Тесты идут через `dotnet test` — они ссылаются на CFTools.Core (чистый net8.0).

## Cloudflare API

Base URL: `https://api.cloudflare.com/client/v4/` (trailing slash обязателен для HttpClient.BaseAddress!)

Headers: `X-Auth-Email` + `X-Auth-Key`

Endpoints:
```
GET    user                          → верификация
GET    accounts                      → список аккаунтов
GET    zones?account.id=X&page=P     → список зон
GET    zones?name=domain.com         → preflight-проверка
POST   zones                         → создание зоны
POST   zones/{id}/purge_cache        → очистка кэша
DELETE zones/{id}                    → удаление зоны
```

**Все endpoints без leading slash** — иначе HttpClient заменит path из BaseAddress.

## Ключевые паттерны

### MVVM (CommunityToolkit.Mvvm 8.4)

Используем **partial properties** (не fields!) для WinRT-совместимости:
```csharp
[ObservableProperty]
public partial string Email { get; set; } = string.Empty;

[RelayCommand]
private async Task ConnectAsync() { ... }
```

XAML: `{x:Bind ViewModel.Email, Mode=TwoWay}`

### Shared services (App.xaml.cs)

```csharp
App.Api          // CloudflareApi singleton
App.Credentials  // CredentialStore singleton
App.Pool         // RequestPool singleton
App.AuthStateChanged  // event для обновления UI при смене auth
```

### Rate limiting (RequestPool)

- `SemaphoreSlim` для concurrency (max 4, cap 8)
- Exponential backoff: `min(20s, 500ms × 2^attempt) + jitter(30%)`
- Retry-After header → берём значение из CF ответа
- Retry только если `NormalizedError.Retryable == true`
- TCS (TaskCompletionSource) ставится на exception только после исчерпания retry

### Error normalization

CF коды → категории:
- **Auth** (no retry): 10000, 10001, 6003, 6100-6103, 9103, 9106
- **RateLimit** (retry): 429
- **Validation** (skip): 1061 (exists), 1003 (invalid name)
- **Dependency** (blocked): 1099 (has subscription)
- **Network** (retry): 5xx, timeout

### Domain parser

1. ASCII regex → извлечь домены из текста
2. Unicode → Punycode: `IdnMapping`
3. Root-only фильтр (не субдомены)
4. Special SLDs: co.uk, com.br и т.д.
5. Дедупликация + IP-фильтр

### Batch processing

Batch — **ephemeral** (in-memory). Все batch ViewModels используют:
- `DispatcherQueue` для UI-thread safety
- `CancellationTokenSource` для Cancel
- `ProgressBar` (Value/Maximum bindings)
- Per-item StatusText в списках
- Error handling per-task (не прерывает весь batch)

### UI threading

Batch callbacks приходят с пула потоков. Обновление UI:
```csharp
await RunOnUiThreadAsync(() => { /* update ObservableProperties */ });
```

CheckBox в DataTemplate: binding обновляется ПОСЛЕ события.
Используем `DispatcherQueue.TryEnqueue()` для отложенной проверки.

## Code Style

- `.editorconfig` — правила именования, braces, var, namespaces
- `Directory.Build.props` — `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`
- **CSharpier** — авто-форматирование (аналог Prettier)
- Перед коммитом: `dotnet csharpier format src/ tests/`

## Зависимости

| Пакет | Версия | Зачем |
|-------|--------|-------|
| Microsoft.WindowsAppSDK | 1.6.x | WinUI 3 |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM source gen (partial properties) |
| CSharpier | 1.2.6 | Форматирование (local dotnet tool) |

Всё остальное (`HttpClient`, `System.Text.Json`, `IdnMapping`, `SemaphoreSlim`) — встроено в .NET 8.

## Реализованные функции

1. **Auth** — Email + Global API Key → Credential Manager → verify → multi-account selector → switch/disconnect/forget
2. **Bulk Add** — paste из любого источника → smart parser (IDN, dedup, root-only) → preflight → create с прогрессом + cancel. Акцент кнопки переключается Check → Create All
3. **Bulk Purge** — загрузка зон → фильтр → multi-select (non-active disabled) → confirmation dialog → purge с прогрессом + cancel. Статус-бейджи, selection counter
4. **Bulk Delete** — загрузка зон → фильтр → multi-select → danger confirmation → delete с прогрессом + cancel. Авто-перезагрузка после удаления
5. **Cross-page sync** — ZoneListChanged event инвалидирует кэш зон при add/delete. Account switch сбрасывает загруженные данные
6. **Adaptive layout** — NavigationView Auto (Compact/Expanded), NavigationCacheMode, OnNavigatedTo, HyperlinkButton к Auth

## Очередь разработки

P1: App icon + branding assets, Zone List + CSV Export, batch result export
P2: API Token auth (с fallback на Global Key), DNS Import/Export, file logging, i18n (resource keys + plurals)
P3: Bulk SSL Mode, Security Level, Always HTTPS, MSIX packaging for Store

## Logging

- **НИКОГДА не логировать:** API Key, auth headers
- Можно: email, domains, CF error codes, latency, batch progress

## Окружение разработки

- Win11, Visual Studio 2022
- Git + GitHub (repo: github.com/investblog/cftools-win, private)
- Hyper-V VM (Win10/Win11) — тестирование совместимости
