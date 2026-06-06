#!/usr/bin/env python3
"""Benchmark translate service latency."""

import argparse
import statistics
import time

import requests


def main():
    parser = argparse.ArgumentParser(description="Benchmark translate API")
    parser.add_argument("--url", default="http://localhost:8000", help="API base URL")
    parser.add_argument("--source", default="en", help="Source language")
    parser.add_argument("--target", default="vi", help="Target language")
    parser.add_argument(
        "--iterations", type=int, default=100, help="Number of requests"
    )
    parser.add_argument(
        "--mode",
        default="both",
        choices=["fast", "quality", "both"],
        help="Which endpoint to benchmark",
    )
    parser.add_argument(
        "--text",
        default="Hello everyone, welcome to today's meeting.",
        help="Text to translate",
    )
    parser.add_argument(
        "--timeout", type=float, default=30.0, help="HTTP timeout in seconds"
    )
    args = parser.parse_args()

    def run_bench(endpoint: str, payload: dict, label: str) -> None:
        latencies = []
        for _ in range(args.iterations):
            started = time.perf_counter()
            resp = requests.post(
                f"{args.url}{endpoint}", json=payload, timeout=args.timeout
            )
            resp.raise_for_status()
            latencies.append((time.perf_counter() - started) * 1000)

        latencies.sort()
        avg = statistics.mean(latencies)
        p50 = latencies[len(latencies) // 2]
        p95 = latencies[int(len(latencies) * 0.95)]
        p99 = latencies[int(len(latencies) * 0.99)]
        rps = 1000 / avg if avg > 0 else 0

        print(f"\n--- {label} ({args.iterations} iterations) ---")
        print(f"  Avg:   {avg:.2f} ms")
        print(f"  P50:   {p50:.2f} ms")
        print(f"  P95:   {p95:.2f} ms")
        print(f"  P99:   {p99:.2f} ms")
        print(f"  RPS:   {rps:.2f}")

    if args.mode in ("fast", "both"):
        run_bench(
            "/translate/fast",
            {
                "text": args.text,
                "source_lang": args.source,
                "target_lang": args.target,
                "session_id": f"bench-{time.time_ns()}",
                "is_final": True,
            },
            "Fast Mode (greedy, beam_size=1)",
        )

    if args.mode in ("quality", "both"):
        run_bench(
            "/translate",
            {
                "text": args.text,
                "source_lang": args.source,
                "target_lang": args.target,
            },
            "Quality Mode (beam_size=4, post-processing)",
        )


if __name__ == "__main__":
    main()
