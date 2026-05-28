# Sublingual — repo guide for agents

## Stack

- .NET 10, Avalonia UI 11, SukiUI 6.1, MVVM (CommunityToolkit.Mvvm)
- Vosk (local STT), GoogleTranslateFreeApi / LibreTranslate (translation), NAudio (Windows audio)
- macOS audio: C++ ScreenCaptureKit bridge via P/Invoke (`native/macos/ScreenCaptureKitBridge/`)
- Python microservice: `translate-service/` (FastAPI + Marian + CTranslate2)

## Build & run

```bash
dotnet run --project src/Sublingual.App/Sublingual.App.csproj
dotnet build Sublingual.slnx
dotnet build src/Sublingual.App/Sublingual.App.csproj
```

No `run-dev.sh` exists — use `dotnet run` directly.

macOS native bridge (required before macOS packaging):
```bash
bash scripts/build-macos-native.sh
```

## Packaging

```bash
bash scripts/package-macos.sh            # zip
bash scripts/package-macos-app.sh        # .app bundle
bash scripts/sign-macos-app.sh <rid> <identity>
pwsh ./scripts/package-windows.ps1       # Windows zip
```

All packaging builds from `src/Sublingual.App/Sublingual.App.csproj`, self-contained Release.

## Known quirks

- **macOS JIT crash**: self-contained publish on macOS can crash at startup (`libclrjit` abort). Mitigation: `System.Runtime.TieredCompilation=false` in runtimeconfig, or ship with `net9.0` instead of `net10.0`. See `docs/KNOWN-ISSUES-MACOS-JIT-CRASH.md`.
- **macOS debug capture**: set `SUBLINGUAL_DEBUG_CAPTURE=1` or pass `--debug-capture` to skip normal UI and run capture-only.
- **App exits to tray**: closing the main window hides to tray. Right-click tray icon → Exit to quit.
- **No tests exist yet** — no test projects found in the repo.
- **No CI workflows**, no `.editorconfig`, no central package management (`Directory.Packages.props`).

## UI conventions

- Use `SukiUI` components (not raw Avalonia replacements). XAML namespace: `xmlns:suki="https://github.com/kikipoulet/SukiUI"`
- Available SukiUI controls: `SukiWindow`, `SukiSideMenu`, `SukiSideMenuItem`, `SukiDialogHost`, `SukiToastHost`, `GlassCard`, `SettingsLayout`, `Loading`, `BusyArea`, etc. See `docs/SUKI-UI.md`.
- Standard controls (Button, TextBox, ComboBox, etc.) are Avalonia controls styled by SukiUI.
- `AvaloniaUseCompiledBindingsByDefault=true` — use compiled bindings in XAML.

## Solution layout (8 projects)

| Project | Role |
|---|---|
| `Sublingual.App` | Main desktop app (WinExe entrypoint) |
| `Sublingual.Desktop` | Desktop-specific wiring (depends on Application, Infrastructure, Interop, Shared) |
| `Sublingual.UI` | Shared UI views/viewmodels |
| `Sublingual.Application` | App-level contracts |
| `Sublingual.Domain` | Domain models & interfaces |
| `Sublingual.Infrastructure` | Implementations (NAudio, etc.) |
| `Sublingual.Interop` | P/Invoke native interop |
| `Sublingual.Shared` | Shared utilities (leaf dependency) |

All projects target `net10.0`, `ImplicitUsings` enabled, `Nullable` enabled.

## App data

Stored under `~/.sublingual/` — settings, sessions, STT models. Default: `en`→`vi`.

## Translation pipeline pitfall

`AudioCaptureDebugSession` currently awaits translation inside the capture pipeline semaphore (`_pipelineGate`). This couples network latency to audio processing. The planned fix is **Stable + Draft** model with a dedicated `TranslationScheduler`, debounced partials, and out-of-order protection. See `docs/REALTIME-TRANSLATION-PLAN.md` and `TODO.md`.

## translate-service (Python microservice)

Separate project under `translate-service/` for self-hosted Marian+CTranslate2 translation.

```bash
cd translate-service
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Endpoints: `GET /health`, `GET /models`, `POST /translate`, `POST /translate/batch`, `POST /translate/realtime`.
