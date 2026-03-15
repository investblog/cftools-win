# Changelog

## v1.1.0 — 2026-03-15

### Multi-account & UX overhaul

**Auth:**
- Multi-account selector — ComboBox when multiple accounts under one login
- Switch account without disconnecting
- Separate Disconnect (keeps credentials) and Forget (clears stored credentials)
- Auth page shows where to get API Key and why Global API Key is required
- Help text hidden after successful connection
- App starts on Auth page instead of Add Domains

**Bulk Delete:**
- Zone filter (search by name), matching Purge Cache
- Confirmation dialog with danger-styled Delete button
- Auto-reload zones after batch deletion
- Per-zone status tracking (Queued → Deleting → Deleted/Failed/Cancelled)

**Bulk Purge:**
- Confirmation dialog before purge with correct messaging
- Status badges on zone list (active/pending)
- Non-active zones shown but disabled (checkbox blocked, row dimmed, tooltip)
- Select All skips non-active zones

**Cross-page sync:**
- Zone list changes (add/delete) invalidate cached data on other pages
- Account switch resets loaded zones with appropriate messaging
- Pending account invalidation applied after batch completes

**Layout & Navigation:**
- Adaptive NavigationView: Expanded (850px+), Compact (below), never Minimal
- Compact nav pane (200px) with truncated email label
- Consistent Padding=16 across all pages
- Compact CheckBox (MinWidth=0, Padding=0) for zone lists
- ListView items stretch with right-aligned badges (MaxWidth=580)
- Selection counter: "X of Y selected"
- HyperlinkButton to Auth page when not connected
- NavigationCacheMode=Required on all pages (preserves state)
- OnNavigatedTo instead of Loaded for reliable zone auto-loading

**Branding:**
- Renamed from CFTools to Cloudflare Tools
- About page: link to browser extension, accurate description
- Domain input placeholder describes all parser capabilities

**RequestPool fix:**
- Cancel() now properly completes queued tasks as canceled (fixes hang)
- New test: Cancel_CompletesQueuedTasksAsCanceled

**Danger button styling:**
- Delete Selected, Forget credentials — red with proper hover/pressed states
- Lightweight styling via Button.Resources (WinUI 3 pattern)

## v1.0.0 — 2026-03-10

### Initial release

- Authentication with Email + Global API Key (Windows Credential Manager)
- Bulk Add Domains with smart parser (IDN, dedup, root-only filter)
- Bulk Purge Cache with zone filter and multi-select
- Bulk Delete Zones with multi-select
- Rate-limited request pool with exponential backoff
- Settings page (concurrency, retries, theme)
- Cloudflare orange branding
