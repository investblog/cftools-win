# CLAUDE.md

Инструкция для работы с проектом CFTools for Windows.

## Что это

Windows-порт браузерного расширения [cloudflare-tools](W:\Projects\cloudflare-tools) — десктопное приложение для bulk-операций с Cloudflare зонами.

**Имя продукта:** «Cloudflare Tools» (с v1.2.0; в 1.1.x было «CFTools» после отказа Store по 10.1.1.1). Внутренние идентификаторы (namespace `CFTools`, exe, Credential Manager resource, папка настроек) не переименовываются.

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
│   │   ├── ZonesPage.xaml/.cs           # Zone list + CSV export
│   │   ├── AddDomainsPage.xaml/.cs
│   │   ├── PurgeCachePage.xaml/.cs
│   │   └── DeleteDomainsPage.xaml/.cs
│   ├── ViewModels/
│   │   ├── AuthViewModel.cs
│   │   ├── ZonesViewModel.cs             # ZoneRow + export commands
│   │   ├── AddDomainsViewModel.cs
│   │   ├── PurgeCacheViewModel.cs
│   │   ├── DeleteDomainsViewModel.cs
│   │   └── ZoneSelection.cs          # Shared zone wrapper for Purge/Delete
│   ├── Services/
│   │   ├── CloudflareApi.cs          # CF API v4 client (Bearer / X-Auth-Key)
│   │   ├── CredentialStore.cs        # Windows Credential Manager (kind encoded in UserName)
│   │   ├── CsvBuilder.cs             # Pure CSV assembly (Core, tested)
│   │   ├── FileExporter.cs           # FileSavePicker + write (UI thread)
│   │   ├── RequestPool.cs            # Rate-limited queue with backoff
│   │   └── DomainParser.cs           # Domain extraction from text
│   ├── Strings/<culture>/Resources.resw  # GENERATED from i18n/*.txt (scripts/build-resw.py)
│   └── Models/
│       ├── CloudflareModels.cs       # API DTOs + state models
│       ├── CredentialModels.cs       # CredentialKind / CfCredential / detector (Core, tested)
│       └── ErrorModels.cs            # Error normalization
├── src/CFTools.Core/                 # Pure .NET 8 library (no WinUI)
│   └── CFTools.Core.csproj           # Links Models/ + Services/ for testing
├── i18n/<lang>.txt                   # UI strings, 12 languages (source of truth for resw)
├── scripts/                          # build-resw.py, check-strings.py (i18n gate), check-store-listings.py
├── tests/CFTools.Tests/              # xUnit (targets net8.0 via Core)
│   ├── DomainParserTests.cs
│   ├── ErrorNormalizerTests.cs
│   ├── RequestPoolTests.cs
│   ├── CredentialDetectorTests.cs
│   ├── CredentialVaultCodecTests.cs  # Credential Manager user-name encoding round-trips
│   ├── CsvBuilderTests.cs
│   └── ApiResponseTests.cs           # messages[] are objects, not strings — 93 tests total
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

# i18n: после правки i18n/*.txt — сгенерировать resw и прогнать страж длины (130% + 8 символов, плейсхолдеры, переносы)
python scripts/build-resw.py && python scripts/check-strings.py

# Запуск на другом языке без смены Windows
src/CFTools/bin/x64/Debug/net8.0-windows10.0.19041.0/CFTools.exe --lang=ja
```

**Важно:** `dotnet build` для WinUI не работает — нужен VS2022 MSBuild из-за XAML tooling.
Тесты идут через `dotnet test` — они ссылаются на CFTools.Core (чистый net8.0).

## Cloudflare API

Base URL: `https://api.cloudflare.com/client/v4/` (trailing slash обязателен для HttpClient.BaseAddress!)

Headers: `X-Auth-Email` + `X-Auth-Key` (Global API Key) **или** `Authorization: Bearer <token>` (cfut_/cfat_). Тип определяется по префиксу секрета (`CredentialDetector`), 37-hex без префикса = legacy Global Key, неизвестный формат: с email → key, без → token.

Endpoints:
```
GET    user                          → верификация (Global API Key)
GET    user/tokens/verify            → верификация user token (cfut_)
GET    accounts/{id}/tokens/verify   → верификация account token (cfat_)
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
7. **Dark theme** — 301-ui design system, theme-aware badge colors (BadgeColors helper), ThemeChanged event для live refresh
8. **About page** — отдельная страница с иконкой, фичами, описанием 301.st, ссылками
9. **App icon** — SVG → multi-res ICO + PNG, отображается в taskbar/title bar/exe
10. **Domain remove** — кнопка "X" для удаления доменов из preflight перед созданием
11. **Punycode tooltips** — hover на xn-- доменах показывает Unicode оригинал
12. **MSIX packaging** — Package.appxmanifest с Store identity, сборка через MSBuild CLI
13. **InnoSetup installer** — для GitHub Releases (standalone distribution)
14. **Zone List** (v1.2.0) — страница Zones: список зон аккаунта (status/plan/NS), фильтр, Export CSV (видимые) и Export all accounts (по `App.AvailableAccounts`)
15. **API Token auth** (v1.2.0) — cfut_/cfat_ с автоопределением, для cfat_ опциональный Account ID; `App.CurrentEmail` для токенов хранит label «API token xxxxxxxx»
16. **Batch result export** (v1.2.0) — кнопка «Export results» после батча в Add/Purge/Delete → CSV `domain,status,error`
17. **Rate this app** + ссылки на расширения + trademark-дисклеймер на About
19. **i18n, 12 языков** (v1.2.0, по принципу Buho) — XAML через `x:Uid`, код через `Loc.Get/Format`; исходник `i18n/<lang>.txt` → `Strings/<culture>/Resources.resw` (генерируется, коммитится). Страж `scripts/check-strings.py`: паритет ключей, ≤130% длины английской + 8, плейсхолдеры `{n}`, переносы. Язык: Windows → Settings override (`AppSettings.Language`) → `--lang=xx`; применяется через `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride` (работает и без package identity) + `SetProcessPreferredUILanguages`. Манифест объявляет 12 `<Resource Language>`. Сообщения `ErrorNormalizer` (Core) остаются английскими.
18. **301.st tips** (v1.2.0) — контекстное промо своего сервиса: InfoBar после успешного Bulk Add (закрытие запоминается в `AppSettings.AfterCreateTipDismissed`), строка на Zones при наличии не-active зон, строка на Auth. Ссылки через `PromoLinks` с utm_campaign per placement. Выключается в Settings → «Show 301.st tips». Глиф — `Views/Controls/Logo301` (Path из 301-ui `brand/301.svg`).

## Store

- **Partner Center**: MSIX app, identity `301.CloudflareTools`, publisher `CN=BEE1F94B-ABDE-4CF8-9F30-1DF4DAFDAE83`
- **Статус**: опубликовано как «CFTools» (1.1.1), https://apps.microsoft.com/detail/9pn4wf799808
- **v1.2.0 (2026-09-11)**: отправлено на сертификацию вечером 11.09 под именем «Cloudflare Tools» (12 языков листинга, trademark в поле Copyright, notes for certification). Результат смотреть в Partner Center. Было: подать с trademark-дисклеймером первой строкой описания + notes for certification (см. docs/store-listing-en.md). Если отклонят по 10.1.1.1 — фолбэк «CFTools for Cloudflare» (паттерн «X for Y»: зарезервировать имя, поменять DisplayName в манифесте/About/README, пересобрать).
- **Store listings**: 12 языков по принципу Buho (en, ru, de, fr, es, it, pt-br, ja, ko, zh-cn, pl, tr) в `docs/store-listing-<lang>.md`: Description (дисклеймер первой строкой + «интерфейс на английском»), What's new, Product features ≤200 симв., Search terms ≤7×30. Проверка лимитов: `python scripts/check-store-listings.py`. UI локализован на те же 12 языков, манифест объявляет их в `<Resources>`; языки листинга добавляются в Partner Center. На 2026-09-11 в Partner Center загружен только EN.

## Сборка MSIX для Store

```bash
# MSIX пакет (без подписи — Store подпишет сам). SelfContained=true ОБЯЗАТЕЛЬНО: без него в пакете нет
# .NET-рантайма (нет coreclr.dll, нет PackageDependency) и на чистой машине приложение просит установить .NET.
# 1.1.x уходили в Store framework-dependent — это было ошибкой, с 1.2.0 self-contained (~65 МБ msix вместо 31).
"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
  src/CFTools/CFTools.csproj \
  -p:Platform=x64 -p:Configuration=Release -p:RuntimeIdentifier=win-x64 -p:SelfContained=true \
  -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never \
  -p:UapAppxPackageBuildMode=StoreUpload \
  -p:AppxPackageDir=temp/AppPackages/
# Output: src/CFTools/temp/AppPackages/CFTools_<ver>_x64_Test/CFTools_<ver>_x64.msix — путь относителен csproj, НЕ корня репо!
# Для подачи копировать в temp/AppPackages/ (корень), где лежат прошлые версии:
#   cp src/CFTools/temp/AppPackages/CFTools_<ver>_x64_Test/*.msix temp/AppPackages/CFTools_<ver>_x64_Test/
# Partner Center accepts the .msix directly; no .msixupload is produced without mspdbcmf.exe (symbols).

# GitHub / SourceForge assets: installer + zip + SHA256SUMS из self-contained сборки в temp/release/<ver>/
python scripts/make-release.py --build     # --build запускает MSBuild -p:RuntimeIdentifier=win-x64 -p:SelfContained=true
# (ISCC лежит на W:\Program Files\Inno Setup 6\ISCC.exe; setup.iss берёт bin\...\win-x64 по умолчанию)
```

## Релиз: что куда

| Канал | Артефакт | Как |
|---|---|---|
| Microsoft Store | `src/CFTools/temp/AppPackages/CFTools_<ver>_x64_Test/CFTools_<ver>_x64.msix` | Partner Center руками; листинги 12 языков из `docs/store-listing-*.md`, notes for certification из EN |
| GitHub Releases | `temp/release/<ver>/` — setup.exe, zip, SHA256SUMS | тело релиза = `docs/release-notes/v<ver>.md` (первым делом «какой файл качать»); msix НЕ прикладывать (без подписи не ставится, SourceForge сделает его default download) |
| SourceForge | зеркало GitHub Releases | `docs/sourceforge-listing.md`: значения для GitHub Project Importer, тексты под лимиты формы (страж `scripts/check-sourceforge-listing.py`), категории, default download = setup.exe. Сначала публикуется GitHub-релиз, потом импорт |

Подписи кода нет (SmartScreen предупреждает). План как у Buho: SignPath Foundation для OSS.

## Очередь разработки

P1: несколько профилей учётных данных (как в расширении v0.2.0), загрузить 11 переводов листинга в Partner Center
P2: DNS Import/Export, file logging
P3: Bulk SSL Mode, Security Level, Always HTTPS

## Logging

- **НИКОГДА не логировать:** API Key, auth headers
- Можно: email, domains, CF error codes, latency, batch progress

## Окружение разработки

- Win11, Visual Studio 2022
- Git + GitHub (repo: github.com/investblog/cftools-win, private)
- Hyper-V VM (Win10/Win11) — тестирование совместимости
