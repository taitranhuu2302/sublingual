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
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.parent.mkdir(parents=True, exist_ok=True)

    converter_command = shutil.which("ct2-transformers-converter")
    if converter_command is None:
        print(
            "Error: ct2-transformers-converter was not found in PATH. "
            "Install ctranslate2 and ensure the converter CLI is available.",
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
