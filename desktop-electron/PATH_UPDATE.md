# ✅ Data Directory Updated to ~/.sublingual

## Changes Made

The app now uses `~/.sublingual/` as the data directory instead of Electron's default `userData` directory.

### New Directory Structure

```
~/.sublingual/
├── models/              # Whisper model files
│   ├── ggml-tiny.bin
│   ├── ggml-base.bin
│   ├── ggml-small.bin
│   ├── ggml-medium.bin
│   └── ggml-large-v3.bin
└── settings.json        # App settings (persistent)
```

### Files Updated

1. **`src/main/models/model-manager.ts`**
   - Changed: `app.getPath("userData")/models` → `~/.sublingual/models`
   - Added: `import os from "os"`

2. **`src/main/settings/settings-store.ts`**
   - Changed: `app.getPath("userData")/settings.json` → `~/.sublingual/settings.json`
   - Added: Directory creation logic
   - Added: `import os from "os"`

### Benefits

- ✅ **Predictable location**: Always at `~/.sublingual/` regardless of OS
- ✅ **Easy access**: No need to find Electron's userData directory
- ✅ **Portable**: Can be backed up/synced easily
- ✅ **Consistent**: Same location across all platforms

### Download Models

```bash
# Create models directory
mkdir -p ~/.sublingual/models

# Download base model (recommended)
cd ~/.sublingual/models
curl -L https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin -o ggml-base.bin
```

### View Settings

```bash
# Check current settings
cat ~/.sublingual/settings.json

# Example output:
# {
#   "language": "en",
#   "modelId": "base",
#   "audioSourceId": "system-default"
# }
```

### Migration

If you had models/settings in the old location, you can migrate them:

```bash
# Backup old location (macOS example)
OLD_DIR=~/Library/Application\ Support/desktop-electron

# Copy to new location
mkdir -p ~/.sublingual
cp -r "$OLD_DIR/models" ~/.sublingual/ 2>/dev/null
cp "$OLD_DIR/settings.json" ~/.sublingual/ 2>/dev/null
```

## Testing

1. Start the app: `pnpm start`
2. App will automatically create `~/.sublingual/` directory
3. Settings will be saved to `~/.sublingual/settings.json`
4. Models are expected in `~/.sublingual/models/`

The change is backward compatible - if the directory doesn't exist, it will be created automatically.
