import argparse
import time
from pathlib import Path

from engines.stt_vosk import VoskSTTEngine


def load_pcm_chunk(path: Path) -> bytes:
    chunk = path.read_bytes()
    if not chunk:
        raise ValueError("PCM sample file is empty.")
    if len(chunk) % 2 != 0:
        raise ValueError("PCM sample must be 16-bit (even byte length).")
    return chunk


def benchmark(engine: VoskSTTEngine, chunk: bytes, runs: int) -> tuple[float, float]:
    durations_ms: list[float] = []
    for _ in range(runs):
        start = time.perf_counter()
        engine.transcribe_chunk(chunk)
        durations_ms.append((time.perf_counter() - start) * 1000)
    avg_ms = sum(durations_ms) / len(durations_ms)
    max_ms = max(durations_ms)
    return avg_ms, max_ms


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Benchmark Vosk processing latency for a PCM chunk."
    )
    parser.add_argument(
        "--sample",
        required=True,
        type=Path,
        help="Path to 16kHz mono PCM Int16 sample (.pcm) file",
    )
    parser.add_argument(
        "--runs",
        type=int,
        default=10,
        help="Number of benchmark runs (default: 10)",
    )
    args = parser.parse_args()

    sample_chunk = load_pcm_chunk(args.sample)
    engine = VoskSTTEngine()
    avg_ms, max_ms = benchmark(engine, sample_chunk, args.runs)

    print(f"Runs: {args.runs}")
    print(f"Chunk bytes: {len(sample_chunk)}")
    print(f"Avg latency: {avg_ms:.2f} ms")
    print(f"Max latency: {max_ms:.2f} ms")
    if avg_ms < 200:
        print("PASS: average latency is below 200ms.")
    else:
        print("FAIL: average latency is >= 200ms.")


if __name__ == "__main__":
    main()

