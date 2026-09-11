"""i18n gate (Buho's rules, adapted): fails (exit 1) when a translation breaks the budget.

    python scripts/check-strings.py

Per language file i18n/<lang>.txt, against the English master i18n/en.txt:
  * the key set must match exactly (nothing missing, nothing extra);
  * a translation may be at most 130% of the English length + 8 chars
    (menus, buttons and hints must not drift apart between languages — fix by
    trimming the string, never by widening the UI);
  * {n} placeholders must match English;
  * a one-line English string stays one line; multi-line strings keep the line count;
  * files must be valid UTF-8 without a BOM.
Also checks that every generated resw is in sync with its .txt (run build-resw.py).
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "i18n"
LANGS = ["en", "ru", "de", "fr", "es", "it", "pt-br", "ja", "ko", "zh-cn", "pl", "tr"]
CULTURES = {"en": "en-US", "pt-br": "pt-BR", "zh-cn": "zh-Hans"}
BUDGET_RATIO = 1.30
BUDGET_SLACK = 8
PLACEHOLDER = re.compile(r"\{\d+\}")


def parse(path: Path) -> dict[str, str]:
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        sys.exit(f"FAIL: {path.name} has a UTF-8 BOM")
    text = raw.decode("utf-8")  # raises on invalid UTF-8
    entries: dict[str, str] = {}
    for line in text.splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        key, value = line.split("=", 1)
        entries[key.strip()] = value.replace("\\n", "\n")
    return entries


def check(lang: str, en: dict[str, str]) -> list[str]:
    path = SRC / f"{lang}.txt"
    if not path.exists():
        return [f"{lang}: {path.name} missing"]
    tr = parse(path)
    problems: list[str] = []

    for key in en.keys() - tr.keys():
        problems.append(f"{lang}: missing key {key}")
    for key in tr.keys() - en.keys():
        problems.append(f"{lang}: unknown key {key}")

    for key, source in en.items():
        if key not in tr:
            continue
        value = tr[key]
        budget = int(len(source) * BUDGET_RATIO) + BUDGET_SLACK
        if len(value) > budget:
            problems.append(f"{lang}: {key} is {len(value)} chars, budget {budget} (en {len(source)})")
        if sorted(PLACEHOLDER.findall(value)) != sorted(PLACEHOLDER.findall(source)):
            problems.append(f"{lang}: {key} placeholders differ from English")
        if value.count("\n") != source.count("\n"):
            problems.append(f"{lang}: {key} line count differs from English")
        if not value.strip():
            problems.append(f"{lang}: {key} is empty")

    culture = CULTURES.get(lang, lang)
    resw = ROOT / "src" / "CFTools" / "Strings" / culture / "Resources.resw"
    if not resw.exists():
        problems.append(f"{lang}: {resw.relative_to(ROOT)} missing — run scripts/build-resw.py")
    else:
        text = resw.read_text(encoding="utf-8")
        names = set(re.findall(r'<data name="([^"]+)"', text))
        expected = {k.replace('"', "&quot;") for k in tr}
        if names != expected:
            problems.append(f"{lang}: resw out of sync with {path.name} — run scripts/build-resw.py")
    return problems


def main() -> int:
    en = parse(SRC / "en.txt")
    problems = [p for lang in LANGS for p in check(lang, en)]
    for p in problems:
        print(p)
    print(f"[check-strings] {'OK' if not problems else 'FAIL'} — {len(LANGS)} languages, {len(en)} keys, {len(problems)} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
