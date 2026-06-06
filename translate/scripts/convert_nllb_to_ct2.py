#!/usr/bin/env python3
"""Convert NLLB-200 HuggingFace model to CTranslate2 format."""

import argparse
import shutil
import sys
from pathlib import Path

import ctranslate2
from transformers import AutoTokenizer


def main():
    parser = argparse.ArgumentParser(
        description="Convert NLLB-200 model to CTranslate2"
    )
    parser.add_argument(
        "--hf_model",
        required=True,
        help="HuggingFace model ID (e.g. facebook/nllb-200-distilled-600M)",
    )
    parser.add_argument(
        "--output_dir",
        required=True,
        help="Output directory for CTranslate2 model",
    )
    parser.add_argument(
        "--quantization",
        default="int8",
        choices=["int8", "int8_float16", "float16", "float32"],
        help="Quantization type (default: int8)",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite existing output directory",
    )
    args = parser.parse_args()

    output_dir = Path(args.output_dir)

    if output_dir.exists():
        if args.force:
            print(f"Removing existing directory: {output_dir}")
            shutil.rmtree(output_dir)
        else:
            print(
                f"Output directory {output_dir} already exists. "
                "Use --force to overwrite."
            )
            sys.exit(1)

    print(f"Converting {args.hf_model} to CTranslate2 ({args.quantization})...")

    converter = ctranslate2.converters.TransformersConverter(
        model_name_or_path=args.hf_model,
    )
    converter.convert(str(output_dir), quantization=args.quantization, force=True)

    print(f"Done. CTranslate2 model saved to {output_dir}")


if __name__ == "__main__":
    main()
