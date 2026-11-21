# Changelog

## v1.2.3 - 2026-11-22

### Store certification fixes (reports 6cab5fcf… and cea3f4fc…)

- About page: removed the "Source code on GitHub" link and the Edge/Chrome/Firefox browser-extension links from Links (policy 10.1.5 - no promotion of software distribution outside the Store). The only remaining links are "Rate this app in Microsoft Store" and the publisher's 301.st web service (no software download)
- GitHub Releases no longer ship installer binaries; the Microsoft Store is the only distribution channel
- Added a dedicated Cloudflare demo account for certification testing; credentials go into Partner Center → Notes for certification (policy 10.3.1 - test account), see `docs/store-listing-en.md`

## v1.1.0 - 2026-03-15

### Multi-account and UX overhaul

**Auth:**
- Multi-account selector for users with multiple Cloudflare accounts under one login
- Switch account without disconnecting
- Separate Disconnect (keeps credentials) and Forget (clears stored credentials)
- Clear guidance on where to find the Global API Key and why it is required
- App starts on the Auth page instead of Add Domains

**Bulk Delete:**
- Zone filter that matches the Purge Cache workflow
- Confirmation dialog with danger-styled Delete button
- Auto-reload after batch deletion
- Per-zone status tracking: Queued -> Deleting -> Deleted / Failed / Cancelled

**Bulk Purge:**
- Confirmation dialog with clearer wording
- Status badges on the zone list
- Non-active zones remain visible but cannot be purged
- Select All skips non-active zones

**Cross-page sync:**
- Zone list changes invalidate cached data on other pages
- Account switch resets stale page state with clear messaging
- Pending account invalidation is applied after a batch completes

**Layout and navigation:**
- Adaptive NavigationView with compact and expanded layouts
- Compact nav pane with truncated auth label
- Consistent `Padding=16` across pages
- Compact CheckBox styling for zone lists
- ListView items stretch cleanly with aligned badges
- Selection counter: `X of Y selected`
- Hyperlink shortcut back to Auth when not connected
- `NavigationCacheMode=Required` on all main pages

**Visual polish:**
- Dark theme inspired by the 301-ui design system
- App icon for the window, taskbar, and executable
- Punycode tooltip and per-domain remove action in Add Domains
- Theme-aware badges that refresh live when the theme changes

**RequestPool fix:**
- `Cancel()` now completes queued tasks as canceled instead of leaving batches hanging
- Added regression test: `Cancel_CompletesQueuedTasksAsCanceled`

**Danger button styling:**
- Delete Selected and Forget credentials use red danger styling with proper hover and pressed states

## v1.0.0 - 2026-03-10

### Initial release

- Authentication with Email and Global API Key via Windows Credential Manager
- Bulk Add Domains with smart parser support for IDN, deduplication, and root-only filtering
- Bulk Purge Cache with zone filter and multi-select
- Bulk Delete Zones with multi-select
- Rate-limited request pool with exponential backoff
- Settings page for concurrency, retries, and theme
- Cloudflare-inspired branding
