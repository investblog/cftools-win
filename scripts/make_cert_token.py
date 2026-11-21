"""Create a scoped Cloudflare user token for Store certification review.

Reads the Global API Key of the demo certification account from
.secrets/cloudflare-globalkey-cert-review (secret-handoff take target; never
print or log the key or the created token value — they must not reach the
transcript). Writes the ready-to-paste Notes-for-certification block to
temp/cert-review-credentials.md and prints only non-secret status.

    python scripts/make_cert_token.py
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
KEY_FILE = ROOT / ".secrets" / "cloudflare-globalkey-cert-review"
OUT_FILE = ROOT / "temp" / "cert-review-credentials.md"
EMAIL = "redirecthops@mailrouting.site"
API = "https://api.cloudflare.com/client/v4"
TOKEN_NAME = "bzm-cert-review-" + datetime.now(timezone.utc).strftime("%Y%m%d")
TTL_DAYS = 180


def call(method: str, path: str, auth_key: str | None = None, bearer: str | None = None, body: dict | None = None) -> dict:
    req = urllib.request.Request(API + path, method=method)
    req.add_header("Content-Type", "application/json")
    if bearer:
        req.add_header("Authorization", f"Bearer {bearer}")
    else:
        req.add_header("X-Auth-Email", EMAIL)
        req.add_header("X-Auth-Key", auth_key or "")
    data = json.dumps(body).encode() if body is not None else None
    try:
        with urllib.request.urlopen(req, data=data, timeout=30) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        detail = e.read().decode(errors="replace")[:400]
        sys.exit(f"API {method} {path} -> HTTP {e.code}: {detail}")


def main() -> int:
    key = KEY_FILE.read_text(encoding="utf-8").strip()
    if not key:
        sys.exit(f"{KEY_FILE}: empty")

    # 1. Validate the Global API Key and list accounts (the app's auth flow).
    accounts = call("GET", "/accounts", auth_key=key)["result"]
    if not accounts:
        sys.exit("Key is valid but the account list is empty.")
    print(f"key OK; accounts visible: {len(accounts)}")

    # 2. Find permission group IDs by name + scope (no hardcoded IDs).
    groups = call("GET", "/user/tokens/permission_groups", auth_key=key)["result"]

    def find_group(name: str, scope: str) -> str:
        for g in groups:
            if g["name"] == name and scope in g.get("scopes", []):
                return g["id"]
        sys.exit(f"permission group not found: {name} ({scope})")

    g_account_settings = find_group("Account Settings Read", "com.cloudflare.api.account")
    g_zone = find_group("Zone Write", "com.cloudflare.api.account.zone")
    g_purge = find_group("Cache Purge", "com.cloudflare.api.account.zone")

    # 3. Create the user token, scoped to the whole demo account.
    #    A token value is shown exactly once; if a same-named token exists
    #    (e.g. a crashed run), delete it first so reruns stay clean.
    for old in call("GET", "/user/tokens?per_page=50", auth_key=key)["result"]:
        if old["name"] == TOKEN_NAME:
            call("DELETE", f"/user/tokens/{old['id']}", auth_key=key)
            print(f"deleted stale token {old['id']} ({TOKEN_NAME})")

    now = datetime.now(timezone.utc)
    body = {
        "name": TOKEN_NAME,
        "not_before": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "expires_on": (now + timedelta(days=TTL_DAYS)).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "policies": [
            {
                "effect": "allow",
                "resources": {"com.cloudflare.api.account.*": "*"},
                "permission_groups": [{"id": g_account_settings}],
            },
            {
                "effect": "allow",
                "resources": {"com.cloudflare.api.account.zone.*": "*"},
                "permission_groups": [{"id": g_zone}, {"id": g_purge}],
            },
        ],
    }
    created = call("POST", "/user/tokens", auth_key=key, body=body)["result"]
    token = created["value"]  # shown only once by the API; never print it
    info = call("GET", f"/user/tokens/{created['id']}", auth_key=key)["result"]
    expires = info.get("expires_on") or info.get("not_after") or "not set"
    print(f"token created: id={info['id']} name={info['name']} expires={expires}")

    # 4. Exercise the token exactly like the app does.
    verify = call("GET", "/user/tokens/verify", bearer=token)["result"]
    accts = call("GET", "/accounts", bearer=token)["result"]
    zones = call("GET", "/zones?per_page=50", bearer=token)["result"]
    print(f"verify: {verify['status']}; accounts={len(accts)}; zones={[z['name'] for z in zones]}")

    # 5. Ready-to-paste block for Partner Center (the token lives only here).
    OUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    OUT_FILE.write_text(
        "# Certification test credentials (Store review) — gitignored, do not commit\n\n"
        f"Email: {EMAIL}\n"
        f"API token: {token}\n\n"
        "Paste into Partner Center -> Submission -> Notes for certification:\n\n"
        "----8<----\n"
        "Test account (dedicated demo account that exists only for certification; "
        "all operations on it are safe and expected):\n"
        f"Email: {EMAIL}\n"
        "API token: " + token + "\n"
        "On the Authentication page paste the token into \"API token or Global API Key\" "
        "and press Connect. No e-mail or Account ID is needed with a user token. "
        "If several accounts are listed, pick any.\n"
        "Suggested test flow: (1) Zones - Load Zones shows the demo zone(s); "
        "(2) Add Domains - paste e.g. \"example.com, sub.example.org\", press Check, "
        "then Create All; (3) Purge Cache - select an active zone and purge; "
        "(4) Delete Domains - select a zone and delete it. All changes affect only "
        "the demo account.\n"
        "The token is scoped to Zone > Zone > Edit, Zone > Cache Purge > Purge and "
        "Account > Account Settings > Read, and expires "
        f"{str(expires)[:10]}.\n"
        "Without credentials, every page still renders and shows a "
        "\"Connect and select an account\" hint.\n"
        "----8<----\n",
        encoding="utf-8",
    )
    print(f"wrote {OUT_FILE.relative_to(ROOT)} (contains the token; gitignored)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
