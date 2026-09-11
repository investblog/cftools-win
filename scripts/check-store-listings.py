"""Validate docs/store-listing-*.md against Partner Center limits.

Checks, per language file:
  - all 12 languages present (Buho's Store set)
  - Description <= 10000 chars, What's new <= 1500 chars
  - Product features: 1..20 items, each <= 200 chars
  - Search terms: <= 7 terms, each <= 30 chars

Usage: python scripts/check-store-listings.py
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

LANGS = ["en", "ru", "de", "fr", "es", "it", "pt-br", "ja", "ko", "zh-cn", "pl", "tr"]
DOCS = Path(__file__).resolve().parent.parent / "docs"

LIMITS = {"description": 10000, "whats_new": 1500, "feature": 200, "features": 20, "term": 30, "terms": 7}


def section(text: str, title: str) -> str:
    match = re.search(rf"^## {re.escape(title)}.*?$\n(.*?)(?=^## |\Z)", text, re.S | re.M)
    return match.group(1).strip() if match else ""


def check(lang: str) -> list[str]:
    path = DOCS / f"store-listing-{lang}.md"
    if not path.exists():
        return [f"{path.name}: missing"]
    text = path.read_text(encoding="utf-8")
    errors: list[str] = []

    description = section(text, "Description")
    if not description:
        errors.append("Description: empty")
    elif len(description) > LIMITS["description"]:
        errors.append(f"Description: {len(description)} chars > {LIMITS['description']}")

    whats_new = section(text, "What's new")
    if len(whats_new) > LIMITS["whats_new"]:
        errors.append(f"What's new: {len(whats_new)} chars > {LIMITS['whats_new']}")

    features = [
        re.sub(r"^\d+\.\s*", "", line).strip()
        for line in section(text, "Product features").splitlines()
        if re.match(r"^\d+\.", line.strip())
    ]
    if not features:
        errors.append("Product features: none")
    if len(features) > LIMITS["features"]:
        errors.append(f"Product features: {len(features)} > {LIMITS['features']}")
    for i, feature in enumerate(features, 1):
        if len(feature) > LIMITS["feature"]:
            errors.append(f"Feature {i}: {len(feature)} chars > {LIMITS['feature']}")

    terms_text = section(text, "Search terms")
    terms = [t.strip() for t in terms_text.replace("\n", " ").split(",") if t.strip()]
    if not terms:
        errors.append("Search terms: none")
    if len(terms) > LIMITS["terms"]:
        errors.append(f"Search terms: {len(terms)} > {LIMITS['terms']}")
    for term in terms:
        if len(term) > LIMITS["term"]:
            errors.append(f"Search term '{term}': {len(term)} chars > {LIMITS['term']}")

    return [f"{path.name}: {e}" for e in errors]


def main() -> int:
    problems = [p for lang in LANGS for p in check(lang)]
    for p in problems:
        print(p)
    print(f"{len(LANGS)} listings checked, {len(problems)} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
