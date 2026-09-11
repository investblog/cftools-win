# SourceForge project listing

**Status: prepared, not yet created.** Form-ready values for the GitHub Project Importer and for
the project admin pages, in the layout the spintax-studio project used
(`W:\Projects\spintax-studio\docs\sourceforge-listing.md`). The field limits below are the ones
read off SourceForge's admin form there on 2026-09-05: Name 40, Short Summary 70, Full
Description 1000 counted with CRLF line endings (a textarea submits CRLF, so every blank-line
separator costs two extra characters). `python scripts/check-sourceforge-listing.py` holds this
file to those limits.

**Order matters.** Publish the GitHub release `v1.2.0` (installer + ZIP + SHA256SUMS) BEFORE
running the importer: the importer copies whatever releases exist, and the newest one becomes the
folder behind the big green Download button. If it runs first, the page advertises 1.1.0.

## GitHub Project Importer (sourceforge.net/p/import_project/github)

| Field | Value |
|---|---|
| GitHub Repository URL | `https://github.com/investblog/cftools-win` |
| Github User / Organization | `investblog` |
| GitHub Repo Name | `cftools-win` |
| SourceForge URL Name | `cloudflare-tools` — fixed forever once created; falls back to `cftools-win` if taken |
| Downloads | **tick** — imports the GitHub releases into Files |
| Source Code | **tick** — a read-only mirror of the repo; the Store listing already says "source on GitHub" |
| Issues | **untick** — issues stay on GitHub; a second tracker splits reports |
| Wiki / Import history | **untick** — the repo has no wiki |

The importer also offers to add a "SourceForge download button" to the GitHub release notes;
leave it off. The release body is hand-written copy that says which file to download.

## Name (40)

```
Cloudflare Tools
```

16 of 40. The imported default is the repository name, `cftools-win`; change it on the first
admin visit.

## Short Summary (70)

```
Bulk zone manager for Cloudflare accounts: add, export, purge, delete
```

69 characters.

## Full Description (1000)

Form-ready, verbatim. Paragraphs separated by one blank line and not hard-wrapped.

```
Cloudflare Tools is an independent Windows app for people who manage dozens or hundreds of Cloudflare zones: paste a list of domains and create them in bulk, list every zone with status, plan and nameservers, export to CSV, purge cache or delete zones in batches with per-zone progress. Sign in with an API token or the Global API Key, switch between accounts, and save the result of any batch as CSV.

Credentials stay in Windows Credential Manager and go only to the Cloudflare API. No telemetry, no analytics, no purchases. Interface in 12 languages, following the Windows display language.

The installer and the ZIP are self-contained: nothing else to install. Also on the Microsoft Store and as a browser extension for Chrome, Edge and Firefox under the same name. Free and open source, MIT.

Cloudflare is a trademark of Cloudflare, Inc. This app is made by 301.st and is not affiliated with, endorsed by, or sponsored by Cloudflare, Inc.
```

## Features

One per row; the form adds a row as you fill the last one. The same claims as the Store
listing's Product features, in SourceForge's narrower column.

1. Bulk add domains: paste lists, CSV, HTML or URLs; root domains extracted, IDN and Punycode handled, duplicates flagged, preflight before creation
2. Zone list with status, plan and nameservers; filter by name; CSV export for one account or all accounts at once
3. Bulk purge cache for selected zones with per-zone progress and cancel
4. Bulk delete zones with a safety confirmation, per-zone status and automatic reload
5. Export batch results to CSV: domain, status, error
6. API token (cfut_ / cfat_) or Global API Key sign-in; the kind is detected automatically
7. Multi-account: sign in once, switch Cloudflare accounts without re-entering credentials
8. Rate-limit aware request queue with retries and backoff
9. 12 languages, follows the Windows display language; dark and light themes
10. Self-contained installer and ZIP, no runtime to install; open source, MIT

## Homepage and support

- **Homepage:** `https://301.st` (the Store listing's website field points there too).
- **Preferred Support Page → URL:** `https://github.com/investblog/cftools-win/issues`.
- **Socials:** leave empty.

## Categorization (/admin/trove)

- **Topic:** `Internet » WWW/HTTP » Site Management`, `System » Networking » DNS`,
  `System » Systems Administration`.
- **License:** `OSI-Approved Open Source » MIT License`.
- **Operating System:** `Windows`.
- **Development Status:** `5 - Production/Stable` (published in the Microsoft Store since March 2026).
- **Intended Audience:** `by End-User Class » Advanced End Users`, `by End-User Class » System Administrators`,
  `by End-User Class » Developers`.
- **Programming Language:** `C#`.
- **User Interface:** `Graphical » Win32 (MS Windows)`.
- **Translations:** `English`, `Russian`, `German`, `French`, `Spanish`, `Italian`,
  `Brazilian Portuguese`, `Japanese`, `Korean`, `Chinese (Simplified)`, `Polish`, `Turkish`.
  Pick the closest entry the form offers if a name differs.

## Downloads — after the import

- Each imported release folder holds the GitHub assets plus `README.md` (the release body,
  rendered under the file list) and the two source archives.
- **Set the default download for Windows** to `CloudflareTools-v<version>-x64-setup.exe` in the
  file manager (the `i` icon on the file → Default Download For → Windows). Left alone,
  SourceForge picks by heuristic and has picked source archives before.
- The MSIX is never a GitHub asset (unsigned outside the Store, so it cannot be installed), so
  nothing needs deleting here.
- Verify from outside, logged out: `https://sourceforge.net/projects/<unixname>/files/latest/download`
  must resolve to the installer.

## Screenshots

Same as the Store listing: `temp/screenshots/` (adddomains, purgecache, deletedomains, settings);
upload the Zones page too once it is captured. 1500×890 works well on the page.

## Keeping releases flowing

The importer is one-shot. For later releases either re-run the import form (Downloads only) or
add a GitHub webhook the narrow way, as spintax-studio did: repository → Settings → Webhooks →
`https://sourceforge.net/p/<unixname>/files-sf/github_webhook`, content type `form`, event
`release` only, secret from the SourceForge Files admin page. It sends events to SourceForge and
grants it nothing.
