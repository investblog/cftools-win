# Cloudflare Tools for Windows

Desktop app for bulk Cloudflare zone management. Windows port of the [Cloudflare Tools](https://microsoftedge.microsoft.com/addons/detail/kklailenhhfnlhbmfaibeonjpdkcpklc) browser extension.

**Stack:** C# / .NET 8 / WinUI 3 (Windows App SDK 1.6)

## Features

- **Bulk Add Domains** — paste domains from any source (plain text, CSV, HTML, email, URLs). Auto-extracts root domains, handles IDN/Punycode, detects duplicates. Preflight check before creation.
- **Bulk Purge Cache** — load all zones, filter, multi-select, purge cache with progress tracking. Non-active zones are shown but disabled.
- **Bulk Delete Zones** — load zones, filter, multi-select with confirmation dialog. Auto-reloads after deletion.
- **Multi-account support** — switch between Cloudflare accounts under the same login.
- **Smart domain parser** — extracts domains from any text input, converts Unicode to Punycode, filters subdomains, deduplicates.
- **Rate-limited request pool** — concurrent API calls with exponential backoff, retry on rate limits, pause/resume/cancel.

## Why Global API Key?

API Tokens have an undocumented quota on new accounts: you can only add as many zones as already exist. Starting from zero — one at a time, making bulk operations impossible. Global API Key has no such limit.

Your key is stored locally in Windows Credential Manager and sent only to the Cloudflare API.

## Getting Started

### Prerequisites

- Windows 10 (1904x) or later
- Visual Studio 2022 with .NET desktop and WinUI workloads

### Build

```bash
# WinUI 3 requires VS2022 MSBuild
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" \
  src/CFTools/CFTools.csproj -p:Platform=x64 -p:Configuration=Debug

# Run
src/CFTools/bin/x64/Debug/net8.0-windows10.0.19041.0/CFTools.exe
```

### Tests

```bash
dotnet test tests/CFTools.Tests/CFTools.Tests.csproj
```

### Code Formatting

```bash
dotnet csharpier format src/ tests/
```

## Usage

1. Open the app — starts on the **Authentication** page
2. Enter your Cloudflare **email** and **Global API Key** ([where to find it](https://dash.cloudflare.com/profile/api-tokens))
3. If multiple accounts — select one from the dropdown
4. Navigate to **Add Domains**, **Purge Cache**, or **Delete Domains**

## Architecture

```
Views (XAML + code-behind)
  └─ ViewModels (CommunityToolkit.Mvvm)
       └─ Services
            ├─ CloudflareApi    — CF API v4 client
            ├─ RequestPool      — rate-limited concurrent queue
            ├─ DomainParser     — domain extraction + IDN
            └─ CredentialStore  — Windows Credential Manager
```

MVVM with source-generated partial properties. Batch operations are ephemeral (in-memory) with per-item status tracking and UI-thread-safe updates via `DispatcherQueue`.

## Project Structure

```
cftools-win/
├── src/CFTools/          # WinUI 3 app
│   ├── Views/            # XAML pages
│   ├── ViewModels/       # MVVM view models
│   ├── Services/         # API, pool, parser, credentials
│   ├── Models/           # DTOs + error normalization
│   └── Converters/       # XAML value converters
├── src/CFTools.Core/     # Pure .NET 8 lib (for testing)
└── tests/CFTools.Tests/  # xUnit tests (60 tests)
```

## Related

- [Cloudflare Tools for Edge](https://microsoftedge.microsoft.com/addons/detail/kklailenhhfnlhbmfaibeonjpdkcpklc) — browser extension
- Author: [301.st](https://301.st)

## License

Private repository.
