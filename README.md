# Cloudflare Tools for Windows

[![Microsoft Store](https://img.shields.io/badge/Microsoft_Store-download-blue?logo=microsoft)](https://apps.microsoft.com/detail/9pn4wf799808)
[![Sponsor](https://img.shields.io/badge/sponsor-301.st-orange)](https://301.st)

Cloudflare Tools for Windows is a desktop app for fast, high-confidence Cloudflare zone work at scale. It brings the core workflows of the [Cloudflare Tools browser extension](https://github.com/investblog/cloudflare-tools) to a native Windows UI with a cleaner, safer bulk-operations experience.

> Cloudflare is a trademark of Cloudflare, Inc. This is an independent, open-source tool by 301.st. It is not affiliated with, endorsed by, or sponsored by Cloudflare, Inc.

**Stack:** C# / .NET 8 / WinUI 3 (Windows App SDK 1.6)

**[Get it from Microsoft Store](https://apps.microsoft.com/detail/9pn4wf799808)**

## Highlights

- **Zone List + CSV export** - browse every zone of an account with status, plan and name servers, filter by name, and export to CSV. One account, or every account visible to your credentials in a single file.
- **Bulk Add Domains** - paste raw exports, CSV, HTML, emails, URLs, or mixed text. The parser extracts root domains automatically, handles IDN and Punycode, flags duplicates and invalid items, and lets you remove individual domains before creation.
- **Bulk Purge Cache** - load zones for the active account, filter quickly, select only what you need, and run a determinate batch with per-zone status.
- **Bulk Delete Zones** - safer destructive workflow with account-aware confirmation, progress tracking, and automatic reload after deletion.
- **Batch result export** - after any add, purge or delete run, save a CSV with per-domain status and error text.
- **API tokens or Global API Key** - sign in with a user token (`cfut_`), an account-owned token (`cfat_`) or the classic Global API Key; the kind is detected from the pasted secret.
- **Multi-account workflow** - sign in once, choose an active account, and switch accounts without re-entering credentials.
- **Responsive Windows UI** - optimized for compact and full-width layouts, with light, dark, and system theme support.
- **Resilient request pipeline** - concurrent API calls with retry handling, rate-limit awareness, and cancellation that keeps UI state consistent.

## Credentials

Either an API token or the Global API Key works:

- **API token** (recommended for day-to-day use) - create it at dash.cloudflare.com > My Profile > API Tokens with `Zone > Zone > Edit`, `Zone > Cache Purge > Purge` and `Account > Account Settings > Read`. Account-owned tokens (`cfat_`) are created under Manage Account > API Tokens and are scoped to one account.
- **Global API Key** - the most reliable option for bulk zone *creation*. API tokens can hit an undocumented limit on some new accounts: you may only be able to add as many zones as already exist, which makes bulk add impossible from zero. The Global API Key does not hit that limit.

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
2. Paste an API token, or your email and Global API Key.
3. If multiple accounts are available, choose the active account for this session.
4. Use Zones, Add Domains, Purge Cache, or Delete Domains. The active account stays visible throughout the app. Any batch can be exported as CSV afterwards.
5. Switch accounts whenever needed without re-entering credentials.

## Architecture

```text
Views (XAML + code-behind)
  -> ViewModels (CommunityToolkit.Mvvm)
     -> Services
        - CloudflareApi    : Cloudflare API v4 client (Bearer token or Global API Key)
        - RequestPool      : rate-aware concurrent work queue
        - DomainParser     : domain extraction, cleanup, IDN support
        - CredentialStore  : Windows Credential Manager integration
        - CsvBuilder       : CSV assembly for zone and batch exports
        - FileExporter     : save-file dialog for exports
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

- [Cloudflare Tools for Edge](https://microsoftedge.microsoft.com/addons/detail/kklailenhhfnlhbmfaibeonjpdkcpklc), [Chrome](https://chromewebstore.google.com/detail/gncbekdjakchefiiahjbjlbhhfijoikp), [Firefox](https://addons.mozilla.org/en-US/firefox/addon/cloudflare-tools/) - browser extension
- Author: [301.st](https://301.st)

## License

MIT
