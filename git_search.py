#!/usr/bin/env python3
import os
import sys
import time
import json
from typing import Dict, Any, List, Optional, Tuple

import urllib.request
import urllib.parse
import urllib.error


GITHUB_API_URL = "https://api.github.com/search/code"

# Simple configuration variables (edit these as needed)
SEARCH_STRING = "com.unity.xr.interaction.toolkit"
PATH_PREFIX = "Packages/"
LANGUAGE = "JSON"

# Optional scope (set to None to ignore)
ORG = None
USER = None
REPO = None  # e.g., "owner/name"

# Pagination
PER_PAGE = 100
MAX_PAGES = 10

"""
This script prints only the number of GitHub code search results.
"""

# Auth token (recommended). Set env var GITHUB_TOKEN or edit here
GITHUB_TOKEN = ""

# Output file for filtered results
OUTPUT_FILE = "results_filtered.json"

def build_search_query(
    search_string: str,
    path_prefix: str,
    language: str,
    org: Optional[str] = None,
    user: Optional[str] = None,
    repo: Optional[str] = None,
) -> str:
    terms: List[str] = []
    # Exact string match in code
    terms.append(f'"{search_string}"')
    # Restrict language
    if language:
        terms.append(f"language:{language}")
    # Restrict path
    if path_prefix:
        # GitHub code search supports path: qualifier (prefix match)
        terms.append(f"path:{path_prefix}")
    # Optional scoping
    if org:
        terms.append(f"org:{org}")
    if user:
        terms.append(f"user:{user}")
    if repo:
        terms.append(f"repo:{repo}")
    return " ".join(terms)


def github_request(url: str, token: Optional[str]) -> Dict[str, Any]:
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": "xui-bench-search-script",
    }
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(req) as resp:
            data = resp.read()
            rate_limit_remaining = resp.headers.get("x-ratelimit-remaining")
            rate_limit_reset = resp.headers.get("x-ratelimit-reset")
            return {
                "status": resp.status,
                "headers": dict(resp.headers),
                "data": json.loads(data.decode("utf-8")),
                "rate_remaining": int(rate_limit_remaining) if rate_limit_remaining else None,
                "rate_reset": int(rate_limit_reset) if rate_limit_reset else None,
            }
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")
        raise RuntimeError(f"GitHub API error {e.code}: {body}")


def search_github_code(
    query: str,
    token: Optional[str],
    per_page: int = 100,
    max_pages: int = 10,
    sleep_on_rate_limit: bool = True,
) -> Tuple[List[Dict[str, Any]], Optional[int]]:
    items: List[Dict[str, Any]] = []
    page = 1
    total_count: Optional[int] = None
    while page <= max_pages:
        params = {
            "q": query,
            "per_page": str(per_page),
            "page": str(page),
            "sort": "indexed",
            "order": "desc",
        }
        url = f"{GITHUB_API_URL}?{urllib.parse.urlencode(params)}"
        try:
            resp = github_request(url, token)
        except RuntimeError as err:
            # Check for secondary rate limit or standard limit
            err_str = str(err)
            if ("rate limit" in err_str.lower() or "abuse detection" in err_str.lower()) and sleep_on_rate_limit:
                # Sleep until reset if available, otherwise backoff
                backoff_seconds = 60
                try:
                    # Best effort parse reset from message
                    backoff_seconds = 60
                except Exception:
                    pass
                time.sleep(backoff_seconds)
                continue
            raise

        data = resp["data"]
        if total_count is None:
            total_count = data.get("total_count")
        items_batch = data.get("items", [])
        items.extend(items_batch)

        # Stop if fewer than requested returned
        if len(items_batch) < per_page:
            break
        page += 1
    return items, total_count


def fetch_repo_stars(full_name: str, token: Optional[str]) -> Optional[int]:
    if not full_name:
        return None
    url = f"https://api.github.com/repos/{full_name}"
    try:
        resp = github_request(url, token)
        repo = resp["data"]
        return repo.get("stargazers_count")
    except Exception:
        return None


def normalize_item(item: Dict[str, Any]) -> Dict[str, Any]:
    repository = item.get("repository", {})
    owner = repository.get("owner", {})
    return {
        "name": item.get("name"),
        "path": item.get("path"),
        "html_url": item.get("html_url"),
        "score": item.get("score"),
        "repository_full_name": repository.get("full_name"),
        "repository_html_url": repository.get("html_url"),
        "owner_login": owner.get("login"),
        "owner_html_url": owner.get("html_url"),
        "repository_default_branch": repository.get("default_branch"),
    }


def print_output(summary: Dict[str, Any]) -> None:
    print(json.dumps(summary, indent=2))


def main() -> int:
    query = build_search_query(
        search_string=SEARCH_STRING,
        path_prefix=PATH_PREFIX,
        language=LANGUAGE,
        org=ORG,
        user=USER,
        repo=REPO,
    )
    items, total_count = search_github_code(
        query=query,
        token=GITHUB_TOKEN,
        per_page=PER_PAGE,
        max_pages=MAX_PAGES,
    )
    # Unfiltered count
    unfiltered_count = total_count if total_count is not None else len(items)

    # Filtered count by repository stars > 5
    repo_to_stars: Dict[str, Optional[int]] = {}
    filtered_count = 0
    filtered_items: List[Dict[str, Any]] = []
    for item in items:
        repo = item.get("repository", {})
        full_name = repo.get("full_name")
        if full_name not in repo_to_stars:
            repo_to_stars[full_name] = fetch_repo_stars(full_name, GITHUB_TOKEN)
        stars = repo_to_stars.get(full_name)
        if stars is not None and stars > 5:
            filtered_count += 1
            filtered_items.append(item)

    # Print two lines: total results, filtered results
    print(unfiltered_count)
    print(filtered_count)

    # Save filtered results to a local JSON file (normalized items)
    try:
        normalized = [normalize_item(i) for i in filtered_items]
        with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
            json.dump(normalized, f, indent=2)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())


