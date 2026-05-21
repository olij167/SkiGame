#!/usr/bin/env python3
"""Search the local UnityDocumentation.zip without extracting it."""

from __future__ import annotations

import argparse
import html
import re
import sys
import zipfile
from pathlib import Path


DEFAULT_ZIP = Path(r"C:/Users/olij1/Downloads/UnityDocumentation.zip")


def normalize_text(value: str) -> str:
    value = re.sub(r"<script\b.*?</script>", " ", value, flags=re.IGNORECASE | re.DOTALL)
    value = re.sub(r"<style\b.*?</style>", " ", value, flags=re.IGNORECASE | re.DOTALL)
    value = re.sub(r"<[^>]+>", " ", value)
    value = html.unescape(value)
    return re.sub(r"\s+", " ", value).strip()


def score_document(path: str, text: str, terms: list[str]) -> int:
    haystack = (path + " " + text).lower()
    score = 0
    for term in terms:
        score += haystack.count(term) * (8 if term in path.lower() else 1)
    return score


def snippet_for(text: str, terms: list[str], width: int) -> str:
    lowered = text.lower()
    positions = [lowered.find(term) for term in terms if lowered.find(term) >= 0]
    start = max(0, min(positions) - width // 3) if positions else 0
    snippet = text[start : start + width]
    if start > 0:
        snippet = "..." + snippet
    if start + width < len(text):
        snippet = snippet + "..."
    return snippet


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("query", help="Search terms, for example: SerializedObject ApplyModifiedProperties")
    parser.add_argument("--zip", default=str(DEFAULT_ZIP), help="Path to UnityDocumentation.zip")
    parser.add_argument("--limit", type=int, default=10, help="Maximum number of results")
    parser.add_argument("--paths", nargs="*", default=[], help="Only include zip paths containing one of these tokens")
    parser.add_argument("--snippet-width", type=int, default=420, help="Characters to show per result")
    args = parser.parse_args()

    zip_path = Path(args.zip)
    if not zip_path.exists():
        print(f"Documentation zip not found: {zip_path}", file=sys.stderr)
        return 2

    terms = [term.lower() for term in re.findall(r"[\w.:-]+", args.query)]
    if not terms:
        print("No searchable terms were provided.", file=sys.stderr)
        return 2

    path_filters = [token.lower() for token in args.paths]
    results: list[tuple[int, str, str]] = []

    with zipfile.ZipFile(zip_path) as archive:
        for info in archive.infolist():
            path = info.filename
            lowered_path = path.lower()
            if not lowered_path.endswith((".html", ".htm", ".md", ".txt")):
                continue
            if path_filters and not any(token in lowered_path for token in path_filters):
                continue

            try:
                raw = archive.read(info)
                document = raw.decode("utf-8", errors="ignore")
            except (OSError, UnicodeDecodeError, zipfile.BadZipFile):
                continue

            text = normalize_text(document)
            score = score_document(path, text, terms)
            if score > 0:
                results.append((score, path, snippet_for(text, terms, args.snippet_width)))

    results.sort(key=lambda item: item[0], reverse=True)
    for index, (score, path, snippet) in enumerate(results[: args.limit], start=1):
        print(f"{index}. score={score} {path}")
        print(snippet)
        print()

    if not results:
        print("No matches found.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
