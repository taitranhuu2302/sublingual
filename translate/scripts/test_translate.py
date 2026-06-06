#!/usr/bin/env python3
"""Test the translate service API endpoints."""

import argparse
import json
import sys

import requests


def main():
    parser = argparse.ArgumentParser(description="Test translate API")
    parser.add_argument("--url", default="http://localhost:8000", help="API base URL")
    parser.add_argument("--source", default="en", help="Source language")
    parser.add_argument("--target", default="vi", help="Target language")
    parser.add_argument("--text", required=True, help="Text to translate")
    parser.add_argument(
        "--mode", default="quality", choices=["fast", "quality"], help="Translation mode"
    )
    parser.add_argument("--batch", action="store_true", help="Send as batch")
    parser.add_argument("--timeout", type=float, default=30.0, help="HTTP timeout in seconds")
    args = parser.parse_args()

    if args.mode == "fast":
        endpoint = f"{args.url}/translate/fast"
        payload = {
            "text": args.text,
            "source_lang": args.source,
            "target_lang": args.target,
            "session_id": "test-session",
            "is_final": True,
        }
    else:
        endpoint = f"{args.url}/translate"
        if args.batch:
            payload = {
                "text": args.text.split("|"),
                "source_lang": args.source,
                "target_lang": args.target,
            }
        else:
            payload = {
                "text": args.text,
                "source_lang": args.source,
                "target_lang": args.target,
            }

    print(f"POST {endpoint}")
    print(f"Payload: {json.dumps(payload, indent=2, ensure_ascii=False)}")

    try:
        resp = requests.post(endpoint, json=payload, timeout=args.timeout)
        resp.raise_for_status()
    except requests.RequestException as exc:
        print(f"Request failed: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc

    data = resp.json()
    print(f"Response ({resp.elapsed.total_seconds() * 1000:.1f}ms):")
    print(json.dumps(data, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
