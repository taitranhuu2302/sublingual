# Electron App Packaging Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Package the NERIS Sublingual Electron app as ZIP archives for macOS and Windows with correct native library bundling, proper app metadata, and icons.

**Architecture:** Use Electron Forge with Vite plugin (already configured). Output ZIP format for both macOS (`.app` inside ZIP) and Windows (`.exe` + resources inside ZIP). Fix native dylib path resolution for packaged mode, add missing `native/` directory to `extraResource`, and set proper app identity (name, icon, description, appBundleId).

**Tech Stack:** Electron 42, Electron Forge 7, Vite 5, TypeScript 4.5, pnpm

---

## High-Level Summary

Currently `pnpm run make` fails in production because:

1. **ScreenCaptureKit dylib** (`screencapture-mac.ts`) uses a hardcoded relative path (`../../native/...`) that resolves inside the asar archive in production — macOS `dlopen` cannot load dylibs from inside asar.
2. **`forge.config.ts`** only has `extraResource: ["bin"]` — the `native/` directory containing the ScreenCaptureKit dylib is not bundled at all.
3. **App metadata** (`productName: "desktop"`, no icon, no appBundleId) — the packaged app would appear as "desktop.app" with a generic Electron icon.
4. **No macOS DMG maker** — only ZIP for macOS, which is not the standard distribution format.
5. **`vosk-worker.ts`** `getLibDir()` function falls back to `process.cwd() + "bin/vosk"` if env vars are missing — this may point to wrong directory in packaged app.

---

### Task 1: Fix ScreenCaptureKit dylib path for packaged app

**Files:**
- Modify: `desktop/src/main/audio/screencapture-mac.ts:4-7`
- Modify: `desktop/src/main/audio/screencapture-mac.ts:1` (add import)

**Problem:** `__dirname` in packaged app points inside the asar archive. `dlopen()` cannot load dylibs from inside asar. Must use `process.resourcesPath` + `extraResource` instead.

- [ ] **Step 1: Add Electron import and fix LIB_PATH**

Replace the `path` import (line 1-2) and `LIB_PATH` constant (line 4-7) in `desktop/src/main/audio/screencapture-mac.ts`:

```typescript
import koffi from "koffi";
import path from "path";
import { app } from "electron";

function getNativeLibPath(): string {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "native", "screencapture-mac", "libScreenCaptureKitBridge.dylib");
  }
  return path.join(__dirname, "../../native/screencapture-mac/libScreenCaptureKitBridge.dylib");
}

const LIB_PATH = getNativeLibPath();
```

- [ ] **Step 2: Verify the import is used only on line 4 area and remove old lines**

The rest of the file (lines 9-132) remains unchanged. The `koffi.load(LIB_PATH)` call at line 24 automatically uses the corrected path.

- [ ] **Step 3: Run TypeScript check to verify compilation**

Run: `npx tsc --noEmit --skipLibCheck 2>&1 | grep -v node_modules | head -10`

Expected: No errors in our source files.

- [ ] **Step 4: Commit**

```bash
git add desktop/src/main/audio/screencapture-mac.ts
git commit -m "fix: resolve ScreenCaptureKit dylib path correctly in packaged app"
```

---

### Task 2: Fix vosk-worker.ts lib path fallback for production

**Files:**
- Modify: `desktop/src/main/asr/vosk-worker.ts:6-12`

**Problem:** `getLibDir()` falls back to `path.join(process.cwd(), "bin", "vosk")` when `RESOURCES_PATH` and `APP_PATH` env vars are empty. In production, `process.cwd()` is `/` (or wherever the user launched from), not the app bundle. The env vars are set by `vosk-process.ts` but only if the fork succeeded with the correct `execPath` — on some platforms the `execPath` fallback might skip env passage.

- [ ] **Step 1: Use the same pattern as vosk-bindings.ts — check process.resourcesPath directly**

Replace the `getLibDir()` function (lines 6-12) in `desktop/src/main/asr/vosk-worker.ts`:

```typescript
function getLibDir(): string {
  const appPath = process.env.APP_PATH;
  const resourcesPath = process.env.RESOURCES_PATH;
  if (resourcesPath) return path.join(resourcesPath, "bin", "vosk");
  if (appPath) return path.join(appPath, "bin", "vosk");
  if (process.resourcesPath) return path.join(process.resourcesPath, "bin", "vosk");
  return path.join(process.cwd(), "bin", "vosk");
}
```

This adds `process.resourcesPath` as an additional fallback — Electron sets this automatically in packaged apps.

- [ ] **Step 2: Run TypeScript check**

Run: `npx tsc --noEmit --skipLibCheck 2>&1 | grep -v node_modules | head -10`

Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/asr/vosk-worker.ts
git commit -m "fix: add process.resourcesPath fallback for Vosk lib discovery in worker"
```

---

### Task 3: Update forge.config.ts — extraResource, app icon, macOS config, ZIP makers for both platforms

**Files:**
- Modify: `desktop/forge.config.ts` (entire file)

**Problem:** `extraResource` only bundles `bin/`. Missing: `native/` directory, app icon, appBundleId, and macOS-specific settings. Need ZIP format for both macOS and Windows.

- [ ] **Step 1: Rewrite forge.config.ts for ZIP output on macOS+Windows**

Replace the entire `desktop/forge.config.ts` with:

```typescript
import type { ForgeConfig } from '@electron-forge/shared-types';
import { MakerSquirrel } from '@electron-forge/maker-squirrel';
import { MakerZIP } from '@electron-forge/maker-zip';
import { MakerDeb } from '@electron-forge/maker-deb';
import { MakerRpm } from '@electron-forge/maker-rpm';
import { VitePlugin } from '@electron-forge/plugin-vite';
import { FusesPlugin } from '@electron-forge/plugin-fuses';
import { FuseV1Options, FuseVersion } from '@electron/fuses';
import { AutoUnpackNativesPlugin } from '@electron-forge/plugin-auto-unpack-natives';

const config: ForgeConfig = {
  packagerConfig: {
    name: 'NERIS Sublingual',
    executableName: 'neris-sublingual',
    asar: true,
    icon: 'assets/logo',
    appBundleId: 'com.neris.sublingual',
    appCategoryType: 'public.app-category.utilities',
    extraResource: ["bin", "native"],
  },
  rebuildConfig: {},
  makers: [
    new MakerSquirrel({}),
    new MakerZIP({}, ['darwin', 'win32']),
    new MakerRpm({}),
    new MakerDeb({}),
  ],
  plugins: [
    new AutoUnpackNativesPlugin({}),
    new VitePlugin({
      build: [
        {
          entry: 'src/main.ts',
          config: 'vite.main.config.ts',
          target: 'main',
        },
        {
          entry: 'src/main/asr/vosk-worker.ts',
          config: 'vite.main.config.ts',
          target: 'main',
        },
        {
          entry: 'src/preload.ts',
          config: 'vite.preload.config.ts',
          target: 'preload',
        },
        {
          entry: 'src/overlay/overlay-preload.ts',
          config: 'vite.preload.config.ts',
          target: 'preload',
        },
      ],
      renderer: [
        {
          name: 'main_window',
          config: 'vite.renderer.config.ts',
        },
      ],
    }),
    new FusesPlugin({
      version: FuseVersion.V1,
      [FuseV1Options.RunAsNode]: false,
      [FuseV1Options.EnableCookieEncryption]: true,
      [FuseV1Options.EnableNodeOptionsEnvironmentVariable]: false,
      [FuseV1Options.EnableNodeCliInspectArguments]: false,
      [FuseV1Options.EnableEmbeddedAsarIntegrityValidation]: true,
      [FuseV1Options.OnlyLoadAppFromAsar]: true,
    }),
  ],
};

export default config;
```

Key changes:
- `packagerConfig.name`: `'NERIS Sublingual'` — the .app bundle name
- `packagerConfig.executableName`: `'neris-sublingual'` — the binary name
- `packagerConfig.icon`: `'assets/logo'` — uses existing logo.icns/logo.ico/logo.png in assets/
- `packagerConfig.appBundleId`: `'com.neris.sublingual'`
- `packagerConfig.appCategoryType`: `'public.app-category.utilities'`
- `packagerConfig.extraResource`: now includes both `"bin"` and `"native"`
- `MakerZIP`: configured for both `darwin` and `win32` platforms
- `MakerSquirrel`: kept for Windows .exe installer option (can be removed if only ZIP needed)

- [ ] **Step 2: Remove @electron-forge/maker-dmg dependency if installed**

If `@electron-forge/maker-dmg` was previously installed, remove it (not needed for ZIP):

Run: `pnpm remove -D @electron-forge/maker-dmg 2>/dev/null; true`

Expected: No error (if not installed, command is ignored).

- [ ] **Step 3: Verify TypeScript compilation**

Run: `npx tsc --noEmit --skipLibCheck 2>&1 | grep -v node_modules | head -10`

Expected: No errors in forge.config.ts.

- [ ] **Step 4: Commit**

```bash
git add desktop/forge.config.ts
git commit -m "feat: add app identity, native resource bundling, ZIP makers for macOS+Windows"
```

---

### Task 4: Update package.json metadata

**Files:**
- Modify: `desktop/package.json` (lines 1-20)

- [ ] **Step 1: Update metadata fields**

Replace lines 1-20 of `desktop/package.json`:

```json
{
  "name": "neris-sublingual",
  "productName": "NERIS Sublingual",
  "version": "1.0.0",
  "description": "Real-time speech-to-text and translation desktop app with transparent subtitle overlay",
  "main": ".vite/build/main.js",
  "private": true,
  "scripts": {
    "start": "electron-forge start",
    "package": "electron-forge package",
    "make": "electron-forge make",
    "make:mac": "electron-forge make --platform=darwin --arch=x64,arm64",
    "make:win": "electron-forge make --platform=win32 --arch=x64",
    "make:linux": "electron-forge make --platform=linux --arch=x64",
    "publish": "electron-forge publish",
    "lint": "eslint --ext .ts,.tsx ."
  },
  "keywords": ["speech-to-text", "translation", "subtitle", "overlay", "electron"],
  "author": {
    "name": "NERIS",
    "email": "tai.tranhuu2002@gmail.com"
  },
  "license": "MIT",
```

Key changes:
- `name`: `"neris-sublingual"` (npm package name)
- `productName`: `"NERIS Sublingual"` — used for .app bundle, Start menu, etc.
- `description`: meaningful description
- New scripts: `make:mac`, `make:win`, `make:linux` with appropriate `--arch` flags
- `keywords`: relevant search terms
- `author`: updated name

- [ ] **Step 2: Verify JSON syntax**

Run: `node -e "JSON.parse(require('fs').readFileSync('package.json','utf8'))"`

Expected: No output (valid JSON).

- [ ] **Step 3: Commit**

```bash
git add desktop/package.json
git commit -m "chore: update app metadata, add platform-specific make scripts"
```

---

### Task 5: Add out/ to .gitignore (if missing)

**Files:**
- Modify: `desktop/.gitignore` (append)

- [ ] **Step 1: Verify `out/` is in .gitignore**

Run: `grep "^out/" desktop/.gitignore`

If not present, append to `desktop/.gitignore`:

```
# Electron-Forge output
out/
```

(Line 92 already has this. Verify it exists; if so, skip this step.)

- [ ] **Step 2: Commit (only if modified)**

```bash
git add desktop/.gitignore
git commit -m "chore: ensure out/ in gitignore"
```

---

### Task 6: Test packaging (macOS)

- [ ] **Step 1: Clean and build for macOS (arm64)**

Run: `rm -rf out/ && pnpm run make -- --platform=darwin --arch=arm64`

Expected: Creates `out/make/zip/darwin/arm64/NERIS Sublingual-darwin-arm64-1.0.0.zip`. No errors.

- [ ] **Step 2: Verify the ZIP contents**

Run: `unzip -l out/make/zip/darwin/arm64/NERIS\ Sublingual-darwin-arm64-*.zip | head -30`

Expected: Shows `NERIS Sublingual.app/Contents/Resources/bin/`, `native/`, and `app.asar`.

- [ ] **Step 3: Verify native libs in .app**

Run: `ls -la out/make/zip/darwin/arm64/NERIS\ Sublingual-darwin-arm64-*.zip` and extract to check:
```bash
cd /tmp && rm -rf "NERIS Sublingual.app"
unzip -qo "out/make/zip/darwin/arm64/NERIS Sublingual-darwin-arm64-"*.zip
ls "NERIS Sublingual.app/Contents/Resources/native/screencapture-mac/"
ls "NERIS Sublingual.app/Contents/Resources/bin/vosk/"
```

Expected: `libScreenCaptureKitBridge.dylib` exists. Vosk dylibs exist.

- [ ] **Step 4: Launch the packaged app**

Run: `open out/NERIS\ Sublingual-darwin-arm64/NERIS\ Sublingual.app`

Expected: App opens with NERIS branding. May show "unidentified developer" warning (Ctrl+click → Open to bypass). Test transcription with a Vosk model.

- [ ] **Step 5: Build for Windows (cross-compile from macOS)**

Run: `pnpm run make -- --platform=win32 --arch=x64`

Expected: Creates `out/make/zip/win32/x64/NERIS Sublingual-win32-x64-1.0.0.zip`. Note: cross-compiling from macOS to Windows may require `wine` or skip due to native module differences. For full Windows testing, build on a Windows machine.

- [ ] **Step 6: Commit**

No code changes to commit. Verification complete.

---

## Post-Packaging Checklist

After successful packaging:

1. **[ ] App name** — `NERIS Sublingual.app` instead of `desktop.app`
2. **[ ] App icon** — The app shows the logo.icns in Finder/Dock (not default Electron icon)
3. **[ ] Native libs** — Vosk and ScreenCaptureKit work in the packaged app
4. **[ ] DMG output** — `NERIS-Sublingual-1.0.0-arm64.dmg` is created
5. **[ ] Overlay** — Subtitle overlay appears when transcription starts
6. **[ ] Code signing** — If distributing outside the App Store, configure Apple Developer ID certificate signing (set `osxSign.identity` in forge.config and provide notarization env vars)

## Known Limitations (Future Work)

- **Code signing / Notarization**: Currently configured as opt-in via env vars. For public distribution, a paid Apple Developer account ($99/year) is required for notarization. Without it, users must right-click → Open to bypass Gatekeeper.
- **Windows installer**: `MakerSquirrel` creates a .exe installer but has no auto-update configured.
- **Auto-update**: Not configured. Would need `electron-updater` + a release server (GitHub Releases, S3, etc.).
- **Linux**: Only `.deb` and `.rpm` are configured. No AppImage or Snap support.
