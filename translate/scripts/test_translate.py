import argparse
import json
import sys

import requests


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Call the translate API and print the result.")
    parser.add_argument("--url", required=True, help="Base URL of the translate service")
    parser.add_argument("--source", default="en", help="Source language code")
    parser.add_argument("--target", default="vi", help="Target language code")
    parser.add_argument("--text", required=True, help="Text to translate")
    parser.add_argument("--timeout", type=float, default=5.0, help="HTTP timeout in seconds")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    url = args.url.rstrip("/") + "/translate"
    payload = {
        "text": args.text,
        "source_lang": args.source,
        "target_lang": args.target,
    }

    try:
        response = requests.post(url, json=payload, timeout=args.timeout)
        response.raise_for_status()
    except requests.RequestException as exc:
        print(f"Request failed: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc

    print(json.dumps(response.json(), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
