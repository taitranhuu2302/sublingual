import argparse
import statistics
import sys
import time

import requests


SAMPLE_TEXTS = [
    "Hello everyone.",
    "Welcome to today's meeting.",
    "Please check the report.",
    "The system is running normally.",
    "We will start in five minutes.",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Benchmark the translate API.")
    parser.add_argument("--url", required=True, help="Base URL of the translate service")
    parser.add_argument("--source", default="en", help="Source language code")
    parser.add_argument("--target", default="vi", help="Target language code")
    parser.add_argument("--iterations", type=int, default=100, help="Number of requests")
    parser.add_argument("--timeout", type=float, default=5.0, help="HTTP timeout in seconds")
    return parser.parse_args()


def percentile(values: list[float], pct: float) -> float:
    if not values:
        return 0.0

    ordered = sorted(values)
    index = max(0, min(len(ordered) - 1, int(round((pct / 100) * (len(ordered) - 1)))))
    return ordered[index]


def main() -> None:
    args = parse_args()
    if args.iterations <= 0:
        print("Iterations must be greater than 0.", file=sys.stderr)
        raise SystemExit(1)

    url = args.url.rstrip("/") + "/translate"
    latencies_ms: list[float] = []
    started = time.perf_counter()

    session = requests.Session()

    try:
        for index in range(args.iterations):
            text = SAMPLE_TEXTS[index % len(SAMPLE_TEXTS)]
            payload = {
                "text": text,
                "source_lang": args.source,
                "target_lang": args.target,
            }
            request_started = time.perf_counter()
            response = session.post(url, json=payload, timeout=args.timeout)
            response.raise_for_status()
            latencies_ms.append((time.perf_counter() - request_started) * 1000)
    except requests.RequestException as exc:
        print(f"Benchmark request failed: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc

    total_elapsed_sec = time.perf_counter() - started
    avg_latency = statistics.fmean(latencies_ms) if latencies_ms else 0.0
    rps = (len(latencies_ms) / total_elapsed_sec) if total_elapsed_sec > 0 else 0.0

    print(f"total requests: {len(latencies_ms)}")
    print(f"avg latency: {avg_latency:.2f} ms")
    print(f"p50: {percentile(latencies_ms, 50):.2f} ms")
    print(f"p95: {percentile(latencies_ms, 95):.2f} ms")
    print(f"p99: {percentile(latencies_ms, 99):.2f} ms")
    print(f"requests per second: {rps:.2f}")


if __name__ == "__main__":
    main()
