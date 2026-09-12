# Cloudflare Tools — Store Listing (English)

## Description

No additional software required: the .NET 8 runtime and the Windows App SDK are bundled in the package (self-contained).

Tools for Cloudflare brings fast, reliable bulk zone management to your Windows desktop. Built for domain professionals who manage dozens or hundreds of zones across several Cloudflare accounts. It works with your own Cloudflare account through the public Cloudflare API v4.

• Bulk Add Domains — paste domains from any source: plain lists, CSV, HTML, emails, URLs or raw exports. The parser extracts root domains, handles IDN and Punycode, flags duplicates and lets you review the list before anything is created.
• Zone List and CSV export — every zone with status, plan and nameservers; filter by name and export to CSV, for one account or for all accounts at once.
• Bulk Purge Cache — select zones, run a batch purge and watch per-zone progress.
• Bulk Delete Zones — filter, select, confirm in a safety dialog, follow per-zone status; the list reloads when done.
• Batch result export — after any add, purge or delete run, save a CSV with per-domain status and error text.
• API token or Global API Key — sign in with a user token (cfut_), an account-owned token (cfat_) or the classic key; the kind is detected from the pasted secret.
• Multi-account — sign in once, switch accounts without re-entering credentials.
• Dark and light themes, follows Windows or set manually.
• Speaks 12 languages (English, Russian, German, French, Spanish, Italian, Portuguese, Japanese, Korean, Chinese, Polish, Turkish) and follows your Windows display language.

Credentials stay in Windows Credential Manager and are sent only to the Cloudflare API. No telemetry, no analytics, no purchases. Source code on GitHub.

Also available as the Cloudflare Tools browser extension for Chrome, Edge and Firefox. Made by 301.st, an edge redirect management platform.

## What's new

Version 1.2.1:
• Zones page with CSV export (one account or all accounts)
• Sign in with API tokens (cfut_ / cfat_) alongside the Global API Key
• Export batch results (add / purge / delete) to CSV
• Rate this app, extension links and a trademark notice on the About page
• Interface in 12 languages, follows the Windows display language

## Product features (keyword-first, ≤200 chars each)

1. Bulk add domains to Cloudflare: paste lists, CSV, HTML or URLs — root domains extracted, IDN and Punycode handled, duplicates flagged, preflight check before creation
2. Zone list with status, plan and nameservers — filter by name, export to CSV for one account or all accounts at once
3. Bulk purge cache for selected zones with per-zone progress and cancel
4. Bulk delete zones with a safety confirmation, per-zone status and automatic reload
5. Export batch results to CSV: domain, status, error — for your records or a retry
6. API token (cfut_ / cfat_) or Global API Key sign-in, kind detected automatically; secrets stay in Windows Credential Manager
7. Multi-account: sign in once, switch Cloudflare accounts without re-entering credentials
8. Rate-limit aware request queue with retries and backoff; dark and light themes; open source, no telemetry
9. Speaks 12 languages and follows the Windows display language; the language can also be set in Settings

## Search terms (Store — max 7 terms, 30 chars each)

cloudflare bulk, add domains, purge cache, delete zones, zone export csv, cloudflare api token, dns zones

## Short title (50)

```
Tools for Cloudflare
```

## Short description (1000)

```
Bulk zone management for Cloudflare accounts on Windows: add domains from any list, browse and export zones to CSV, purge cache and delete zones in batches. API token or Global API Key, multiple accounts, 12 languages.
```

## Copyright and trademark info (200)

```
Cloudflare® is a registered trademark of Cloudflare, Inc. Tools for Cloudflare is an independent app by 301.st, not affiliated with, endorsed by or sponsored by Cloudflare, Inc. © 2026 301.st, MIT.
```

## Developed by (255)

```
301.st — the edge redirect and domain management platform for Cloudflare: bulk redirects, TDS routing, zone inventory with nameserver checks and expiry tracking. Free tier up to 10 domains. https://301.st
```

## Screenshot captions (temp/screenshots, 200 each)

- `adddomains.png` — Add Domains: paste any list, the parser extracts root domains and checks them before creation
- `purgecache.png` — Purge Cache: select zones, run a batch purge and watch per-zone progress
- `deletedomains.png` — Delete Domains: filter, select, confirm in a safety dialog, follow per-zone status
- `settings.png` — Settings: request pool limits, theme and language

## Notes for certification (Partner Center → Submission → Notes for certification)

Product name and trademark (policy 10.1.1.1):
- Title changed after the 2026-09-11 certification report to "Tools for Cloudflare": the referential "X for Y" form the report asked for, so the title no longer starts with the name of the referenced service. The package DisplayName and all 12 listing languages carry the new title.
- "Cloudflare" is used nominatively, to describe the service the tool works with (a client for the public Cloudflare API v4). The "Copyright and trademark info" field states that Cloudflare is a trademark of Cloudflare, Inc. and that the app is independent; the About page inside the app says the same. The icon and screenshots use our own design, not Cloudflare's logo.
- The same publisher ships the browser extension "Cloudflare Tools" on Chrome Web Store, Edge Add-ons and Firefox Add-ons; the Windows app is its desktop edition. Publisher name and support links point to 301.st; source code: https://github.com/investblog/cftools-win.

Software dependencies (policy 10.2.4.1):
- The package is self-contained. CFTools.runtimeconfig.json declares includedFrameworks = Microsoft.NETCore.App 8.0.30; hostfxr.dll, hostpolicy.dll, coreclr.dll, clrjit.dll and the Windows App SDK runtime ship inside the package. No .NET Desktop Runtime or any other install is required, and the first line of every description says so.
- This submission carries an x64 package and an arm64 package, so ARM devices (Surface Laptop on Snapdragon) run native code rather than the x64 package under emulation.

Localization (policy 10.7):
- The app interface is localized into the 12 languages declared in the package manifest (en, ru, de, fr, es, it, pt-br, ja, ko, zh-Hans, pl, tr); Store listings are provided in the same 12 languages. The language follows the Windows display language and can be overridden in Settings.

Testing (policy 10.3):
- The app requires the tester's own Cloudflare credentials (an API token or Global API Key). A demo Cloudflare account cannot be shared because all operations (create / delete zones, purge cache) modify the account. Without credentials the reviewer can open every page: Zones, Add Domains, Purge Cache, Delete Domains, Settings and About all render and show a "Connect and select an account" hint.
- No purchases, no third-party ads, no analytics, no network access other than https://api.cloudflare.com. The About page and two contextual, user-dismissible tips (switchable off in Settings) link to the publisher's own service, 301.st.
