# CFTools for Windows

[![GitHub release](https://img.shields.io/github/v/release/investblog/cftools-win)](https://github.com/investblog/cftools-win/releases/latest)
[![Sponsor](https://img.shields.io/badge/sponsor-301.st-orange)](https://301.st)

CFTools for Windows is a desktop app for fast, high-confidence Cloudflare zone work at scale. It brings the core workflows of the CFTools browser extension to a native Windows UI with a cleaner, safer bulk-operations experience.

**Stack:** C# / .NET 8 / WinUI 3 (Windows App SDK 1.6)

**[Download latest release](https://github.com/investblog/cftools-win/releases/latest)**

## Highlights

- **Bulk Add Domains** - paste raw exports, CSV, HTML, emails, URLs, or mixed text. The parser extracts root domains automatically, handles IDN and Punycode, flags duplicates and invalid items, and lets you remove individual domains before creation.
- **Bulk Purge Cache** - load zones for the active account, filter quickly, select only what you need, and run a determinate batch with per-zone status.
- **Bulk Delete Zones** - safer destructive workflow with account-aware confirmation, progress tracking, and automatic reload after deletion.
- **Multi-account workflow** - sign in once, choose an active account, and switch accounts without re-entering credentials.
- **Responsive Windows UI** - optimized for compact and full-width layouts, with light, dark, and system theme support.
- **Resilient request pipeline** - concurrent API calls with retry handling, rate-limit awareness, and cancellation that keeps UI state consistent.

## Why Global API Key?

For this app's bulk zone creation workflow, Global API Key is the most reliable option. API Tokens can hit an undocumented limit on some new accounts: you may only be able to add as many zones as already exist, which makes true bulk add impossible from zero.

Credentials are stored locally in Windows Credential Manager and sent only to the Cloudflare API.

## Getting Started

### Prerequisites

- Windows 10 (19041) or later
- Visual Studio 2022 with .NET desktop and WinUI workloads

### Build

From the repo root:

```powershell
dotnet build CFTools.sln
```

### Test

```powershell
dotnet test CFTools.sln
```

### Optional formatting

Formatting follows `.editorconfig` and repo style rules. If you use CSharpier locally:

```powershell
dotnet csharpier format src/ tests/
```

## Usage

1. Open the app. It starts on the Authentication page.
2. Enter your Cloudflare email and Global API Key.
3. If multiple accounts are available, choose the active account for this session.
4. Use Add Domains, Purge Cache, or Delete Domains. The active account stays visible throughout the app.
5. Switch accounts whenever needed without re-entering credentials.

## Architecture

```text
Views (XAML + code-behind)
  -> ViewModels (CommunityToolkit.Mvvm)
     -> Services
        - CloudflareApi    : Cloudflare API v4 client
        - RequestPool      : rate-aware concurrent work queue
        - DomainParser     : domain extraction, cleanup, IDN support
        - CredentialStore  : Windows Credential Manager integration
```

MVVM with source-generated partial properties. Batch workflows keep per-item status in memory and marshal UI updates through `DispatcherQueue`.

## Project Structure

```text
cftools-win/
|-- src/CFTools/          # WinUI 3 app
|   |-- Views/            # XAML pages
|   |-- ViewModels/       # MVVM view models
|   |-- Services/         # API, pool, parser, credentials
|   |-- Models/           # DTOs and shared models
|   `-- Converters/       # XAML value converters
|-- src/CFTools.Core/     # Pure .NET 8 library for testable core logic
`-- tests/CFTools.Tests/  # xUnit tests
```

## Related

- [CFTools for Edge](https://microsoftedge.microsoft.com/addons/detail/kklailenhhfnlhbmfaibeonjpdkcpklc) - browser extension
- Author: [301.st](https://301.st)

## License

MIT
