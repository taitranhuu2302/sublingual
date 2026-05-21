import argparse
import shutil
import subprocess
import sys
from pathlib import Path

from transformers import MarianTokenizer


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert a MarianMT Hugging Face model to CTranslate2 format.",
    )
    parser.add_argument("--hf_model", required=True, help="Hugging Face Marian model name")
    parser.add_argument("--output_dir", required=True, help="Output directory for CT2 model")
    parser.add_argument(
        "--quantization",
        default="int8",
        help="CTranslate2 quantization type, for example int8 or float16",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite output directory if it already exists",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.parent.mkdir(parents=True, exist_ok=True)

    try:
        import torch  # noqa: F401
    except ImportError:
        print(
            "Error: PyTorch is required to convert Hugging Face Marian models with "
            "ct2-transformers-converter. Install torch in the current environment "
            "and rerun this script.",
            file=sys.stderr,
        )
        raise SystemExit(1)

    python_dir = Path(sys.executable).parent
    prefix_dir = Path(sys.prefix)
    converter_candidates = [
        python_dir / "ct2-transformers-converter",
        prefix_dir / "bin" / "ct2-transformers-converter",
        python_dir / "Scripts" / "ct2-transformers-converter.exe",
        python_dir / "Scripts" / "ct2-transformers-converter",
        prefix_dir / "Scripts" / "ct2-transformers-converter.exe",
        prefix_dir / "Scripts" / "ct2-transformers-converter",
    ]

    converter_command = next(
        (str(candidate) for candidate in converter_candidates if candidate.is_file()),
        None,
    )

    if converter_command is None:
        converter_command = shutil.which("ct2-transformers-converter")

    if converter_command is None:
        print(
            "Error: ct2-transformers-converter was not found for the current Python "
            f"environment ({sys.executable}). Install ctranslate2 in that environment "
            "and rerun this script.",
            file=sys.stderr,
        )
        raise SystemExit(1)

    command = [
        converter_command,
        "--model",
        args.hf_model,
        "--output_dir",
        str(output_dir),
        "--quantization",
        args.quantization,
    ]

    if args.force:
        command.append("--force")

    try:
        subprocess.run(command, check=True)
    except subprocess.CalledProcessError as exc:
        print(
            f"Error: model conversion failed for {args.hf_model} with exit code {exc.returncode}.",
            file=sys.stderr,
        )
        raise SystemExit(exc.returncode) from exc

    tokenizer = MarianTokenizer.from_pretrained(args.hf_model)
    tokenizer.save_pretrained(output_dir)

    if not output_dir.exists() or not any(output_dir.iterdir()):
        print(
            f"Error: conversion finished but output directory {output_dir} is empty.",
            file=sys.stderr,
        )
        raise SystemExit(1)

    print(f"Model converted successfully: {args.hf_model} -> {output_dir}")


if __name__ == "__main__":
    main()
