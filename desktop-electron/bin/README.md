# Whisper.cpp Binary

This directory contains the `whisper-cli` binary compiled from [whisper.cpp](https://github.com/ggerganov/whisper.cpp).

## Current Binary

- **Platform**: macOS ARM64
- **File**: `whisper-cli` (844 KB)
- **Version**: whisper.cpp latest
- **Built**: May 29, 2026

## Usage

The binary is used by the Electron app's ASR engine (`src/main/asr/whisper-process.ts`) to perform speech recognition.

```bash
./whisper-cli --model /path/to/model.bin --language en --output-json - < audio.wav
```

## Building for Other Platforms

### macOS / Linux
```bash
cd /path/to/whisper.cpp
make
cp build/bin/whisper-cli /path/to/desktop-electron/bin/
```

### Windows
```bash
cd \path\to\whisper.cpp
cmake -B build
cmake --build build --config Release
copy build\bin\Release\whisper-cli.exe \path\to\desktop-electron\bin\
```

## Required for App to Function

Without this binary, the ASR (speech recognition) functionality will not work.
