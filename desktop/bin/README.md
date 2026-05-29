# Whisper.cpp Binaries

Pre-built binaries from [whisper.cpp](https://github.com/ggerganov/whisper.cpp) for speech recognition.

## Included

| File | Platform | Arch |
|------|----------|------|
| `whisper-cli` | macOS | ARM64 |

## Adding Windows Binary

Build on a Windows machine and copy `whisper-cli.exe` here:

```powershell
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
cmake -B build
cmake --build build --config Release
copy build\bin\Release\whisper-cli.exe \path\to\sublingual\desktop\bin\
```

## How It Works

The app (`src/main/asr/whisper-process.ts`) auto-selects:
- `whisper-cli` on macOS/Linux
- `whisper-cli.exe` on Windows
