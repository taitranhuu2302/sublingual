# Sublingual

Real-time speech-to-text and translation desktop app with a transparent subtitle overlay. Built for meetings, media playback, and live bilingual subtitles.

## Use Cases

- Online meetings (Google Meet, Teams, Zoom) — see live subtitles in your language
- Video and media playback — real-time captions from system audio
- Language learning — bilingual subtitles during study or entertainment

## Tech Stack

| Layer | Technology |
| --- | --- |
| Desktop app | Electron 42 + React 19 + TypeScript |
| UI framework | shadcn/ui + Tailwind CSS 4 |
| Build tooling | Vite 5 + Electron Forge |
| Speech-to-text | whisper.cpp (local, offline) |
| Translation | Google Translate Free API / Local NLLB-200 service |
| macOS audio capture | Native ScreenCaptureKit bridge |

## Architecture

```
System Audio → Audio Capture → whisper.cpp STT → Auto-translate → Overlay Window
                                       ↓                              ↓
                                  Session Storage              Floating Subtitles
```

## Repository Structure

```
sublingual/
├── desktop/              # Electron desktop app (main project)
│   ├── src/
│   │   ├── main/         # Main process (Node.js)
│   │   │   ├── asr/      # Whisper.cpp speech-to-text engine
│   │   │   ├── audio/    # Audio capture (system audio)
│   │   │   ├── ipc/      # IPC handlers (audio, asr, settings, etc.)
│   │   │   ├── models/   # Model manager, downloader, catalog
│   │   │   ├── overlay/  # Overlay window manager
│   │   │   ├── sessions/ # Session recording and storage
│   │   │   ├── settings/ # Settings store (~/.sublingual/settings.json)
│   │   │   └── translation/ # Translation providers
│   │   ├── overlay/      # Overlay renderer (separate BrowserWindow)
│   │   ├── pages/        # React pages (Home, Settings, Sessions)
│   │   ├── components/   # React components + shadcn/ui
│   │   ├── hooks/        # React hooks
│   │   └── types/        # TypeScript type definitions
│   └── bin/              # whisper-cli binary
├── translate/            # Local translation microservice (optional)
│   ├── app/              # FastAPI service (MarianMT + CTranslate2)
│   ├── scripts/          # Model conversion and benchmarking
│   ├── models/           # CTranslate2 model files
│   └── docker/           # Docker deployment
├── native/               # Native platform bridges
│   └── macos/            # ScreenCaptureKit bridge for macOS
├── assets/               # App icons and logos
├── scripts/              # Build and packaging scripts
└── docs/                 # Architecture docs and plans
```

## Quick Start

### Prerequisites

- **Node.js** 20+ and **pnpm**
- **macOS** (ARM64) or **Windows** (needs separate `whisper-cli.exe`, see below)
- **A whisper model** (downloaded from the app, see [Model Download](#model-download))

### 1. Install Dependencies

```bash
cd desktop
pnpm install
```

### 2. Whisper Binary (Windows only)

The `whisper-cli` binary for macOS ARM64 is **already included** in `desktop/bin/`.

For Windows, build and copy the binary:

```powershell
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
cmake -B build
cmake --build build --config Release
copy build\bin\Release\whisper-cli.exe \path\to\sublingual\desktop\bin\
```

The app automatically picks `whisper-cli` on macOS or `whisper-cli.exe` on Windows.

### 3. Run the App

```bash
cd desktop
pnpm start
```

### 4. First Run Setup

1. Open **Settings → Speech** and click **Download Models**
2. Download at least one model (recommended: **Base** for testing, **Small** for daily use)
3. Select the downloaded model in Settings
4. Go to **Home**, select an audio source, and press **Start**

## Model Download

Whisper models are downloaded from Hugging Face and stored in `~/.sublingual/models/`.

| Model | Size | Speed | Accuracy | Recommended For |
| --- | --- | --- | --- | --- |
| Tiny | 75 MB | Fastest | Lower | Quick testing |
| Base | 142 MB | Fast | Good | Getting started |
| Small | 466 MB | Moderate | Better | Daily use |
| Medium | 1.5 GB | Slow | High | Important meetings |
| Large v3 | 3.1 GB | Slowest | Highest | Maximum accuracy |

Models are downloaded directly from the app via **Settings → Speech → Download Models**.

## App Data

All data is stored under `~/.sublingual/`:

```
~/.sublingual/
├── settings.json          # App configuration
├── models/                # Downloaded whisper models
│   ├── ggml-tiny.bin
│   ├── ggml-base.bin
│   └── ...
└── sessions/              # Recorded transcription sessions
    └── 2026-05-30T03-00-00-000Z/
        ├── meta.json      # Session metadata (start time, duration)
        └── transcript.json # Transcript lines with timestamps
```

### Default Settings

| Setting | Default |
| --- | --- |
| Source language | `en` (English) |
| Target language | `vi` (Vietnamese) |
| Translation provider | Google Translate Free API |
| Chunk timing | Balanced (1000ms) |
| Overlay theme | Dark |
| Overlay opacity | 88% |

## Translation Setup

### Google Translate Free API (default)

Works out of the box. No API key required. Uses the free Google Translate endpoint.

### Local NLLB-200 Translation Service

For offline/private translation, the app ships with a bundled NLLB-200 600M translation service. It auto-starts when the app launches and stops on quit.

**Build the service binary (one-time):**

```bash
cd translate

# Create virtual environment and install dependencies
python3 -m venv .venv
source .venv/bin/activate        # Windows: .venv\Scripts\activate
pip install -r requirements.txt

# Build PyInstaller executable → desktop/bin/translate/
bash scripts/build_pyinstaller.sh    # macOS/Linux
# or
.\scripts\build_pyinstaller.ps1      # Windows PowerShell
```

**Download NLLB-200 model (one-time):**

The model (~1.2 GB int8) is downloaded automatically on first run. Alternatively, convert it manually:

```bash
pip install torch  # needed for conversion only
python scripts/convert_nllb_to_ct2.py \
  --hf_model facebook/nllb-200-distilled-600M \
  --output_dir models/ct2/nllb-200-600M \
  --quantization int8
```

Then place the converted model in `~/.sublingual/translate-models/nllb-200-600M/`.

**Run the app:**

```bash
cd desktop
pnpm start
```

The service starts automatically. Check status in **Settings → Translation**.

**Disable auto-start:**

```powershell
# Windows PowerShell
$env:SUBLINGUAL_NO_TRANSLATE=1; pnpm start
```

```bash
# macOS / Linux
SUBLINGUAL_NO_TRANSLATE=1 pnpm start
```

Useful when you're working on frontend only and don't need translation.

Then in the app, go to **Settings → Translation** and select **Local TranslateService** as the provider.

See `translate/README.md` for full API documentation including endpoints, batch translation, and benchmarking.

## Features

- **Real-time STT** — whisper.cpp running locally, no cloud dependency
- **Auto-translation** — each recognized segment is translated automatically
- **Overlay window** — transparent, always-on-top floating subtitles with drag and resize
- **Session recording** — every capture session saves transcript with timestamps
- **Session browser** — review, search, export (TXT/JSON), and delete past sessions
- **Model manager** — download and manage whisper models from the app
- **Configurable chunk timing** — Fast (500ms), Balanced (1000ms), Accurate (2000ms)
- **Settings** — 4 sections: General, Speech, Translation, Overlay

## Development

### Run the app

```bash
cd desktop

# Build translate service binary (first time only)
pnpm run build:translate

# Start the app (with translate service auto-start)
pnpm start

# Start WITHOUT translate service (frontend-only dev)
pnpm run start:no-translate
```

### Build

```bash
cd desktop
pnpm run make        # Build distributable
```

### Package

```bash
cd desktop
pnpm run package     # Package without installer
```

### Project Scripts

```bash
pnpm start              # Run in development mode
pnpm run start:no-translate  # Run without translate service
pnpm run build:translate     # Build PyInstaller translate-service binary
pnpm run lint           # Run ESLint
pnpm run package        # Package the app
pnpm run make           # Build distributable installer
```

## Platform Support

| Feature | macOS | Windows |
| --- | --- | --- |
| Desktop app | ✅ | ✅ |
| Overlay window | ✅ | ✅ |
| System audio capture | ✅ (ScreenCaptureKit) | ✅ |
| Packaging | ✅ (ZIP) | ✅ (Squirrel) |

## Troubleshooting

### "No model selected"

Go to **Settings → Speech → Download Models**, download a model, then select it.

### Whisper binary not found

The binary is included at `desktop/bin/whisper-cli` (macOS ARM64). Windows users need to build `whisper-cli.exe` from [whisper.cpp](https://github.com/ggerganov/whisper.cpp) — see `desktop/bin/README.md`.

### STT not producing output

- Try **Balanced** (1000ms) chunk timing instead of Fast (500ms)
- Make sure the correct audio source is selected
- Check that the whisper model file is not corrupted (re-download if needed)

### Overlay shows white screen

Make sure `overlay.html` exists in the project root (`desktop/overlay.html`), not inside `src/`.

### macOS audio permission

macOS requires Screen Recording permission to capture system audio. Grant it in **System Settings → Privacy & Security → Screen Recording**.

## License

MIT