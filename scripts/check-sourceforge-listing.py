"""Hold docs/sourceforge-listing.md to the SourceForge form limits.

    python scripts/check-sourceforge-listing.py

Limits (read off the admin form's DOM for spintax-studio on 2026-09-05): Name 40, Short
Summary 70, Full Description 1000 — the description counted with CRLF line endings, the way a
textarea submits it. Counts are UTF-16 code units, as a browser counts. Also refuses a
hard-wrapped description paragraph (a textarea keeps every break it is given) and checks that
the feature list is numbered 1..N without gaps.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

DOC = Path(__file__).resolve().parent.parent / "docs" / "sourceforge-listing.md"
LIMITS = {"Name": 40, "Short Summary": 70, "Full Description": 1000}


def units(s: str) -> int:
    return len(s.encode("utf-16-le")) // 2


def fenced(text: str, heading: str) -> str:
    pattern = rf"^## {re.escape(heading)}.*?\n```\n(.*?)\n```"
    matches = re.findall(pattern, text, re.S | re.M)
    if len(matches) != 1:
        sys.exit(f"FAIL: expected exactly one fenced block under '## {heading}', found {len(matches)}")
    return matches[0]


def main() -> int:
    text = DOC.read_text(encoding="utf-8")
    problems: list[str] = []

    name = fenced(text, "Name")
    if units(name) > LIMITS["Name"]:
        problems.append(f"Name: {units(name)} > {LIMITS['Name']}")

    summary = fenced(text, "Short Summary")
    if units(summary) > LIMITS["Short Summary"]:
        problems.append(f"Short Summary: {units(summary)} > {LIMITS['Short Summary']}")

    description = fenced(text, "Full Description")
    crlf = units(description.replace("\n", "\r\n"))
    if crlf > LIMITS["Full Description"]:
        problems.append(f"Full Description: {crlf} (CRLF) > {LIMITS['Full Description']}")
    paragraphs = description.split("\n\n")
    for i, p in enumerate(paragraphs, 1):
        if "\n" in p:
            problems.append(f"Full Description: paragraph {i} is hard-wrapped")

    features = re.search(r"^## Features.*?\n\n(.*?)\n\n## ", text, re.S | re.M)
    if not features:
        problems.append("Features: section not found")
    else:
        numbers = [int(m) for m in re.findall(r"^(\d+)\. ", features.group(1), re.M)]
        if numbers != list(range(1, len(numbers) + 1)) or not numbers:
            problems.append(f"Features: ordinals are {numbers}, expected 1..N")

    for p in problems:
        print(p)
    print(
        f"[check-sourceforge-listing] {'OK' if not problems else 'FAIL'} — "
        f"name {units(name)}/40, summary {units(summary)}/70, description {crlf}/1000 (CRLF)"
    )
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
