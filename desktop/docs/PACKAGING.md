# NERIS Sublingual — Packaging Guide

## Đầu ra

| Nền tảng | Định dạng | Tên file |
|----------|-----------|----------|
| macOS | ZIP chứa `.app` | `NERIS Sublingual-darwin-{arch}-{version}.zip` |
| Windows | ZIP chứa `.exe` + resources | `NERIS Sublingual-win32-x64-{version}.zip` |

## Build nhanh

```bash
cd desktop

# macOS (arm64 + x64)
pnpm make:mac

# Windows (x64)
pnpm make:win

# Linux (x64) — yêu cầu build trên máy Linux
pnpm make:linux
```

Output nằm ở `desktop/out/make/zip/{platform}/{arch}/`.

## Build tuỳ chỉnh

```bash
# Build 1 platform, 1 arch
pnpm exec electron-forge make --platform=darwin --arch=arm64

# Build với version khác (sửa trong package.json trước)
pnpm exec electron-forge make --platform=win32 --arch=x64

# Build toàn bộ platform (mặc định)
pnpm make
```

## Cấu trúc thư mục đầu ra

```
out/
├── NERIS Sublingual-darwin-arm64/
│   └── NERIS Sublingual.app/          # App bundle (macOS)
│       └── Contents/
│           ├── Info.plist
│           ├── MacOS/neris-sublingual
│           └── Resources/
│               ├── app.asar           # Mã nguồn đã đóng gói
│               ├── bin/vosk/          # Vosk native libs
│               ├── native/screencapture-mac/  # ScreenCaptureKit dylib
│               └── electron.icns      # App icon
└── make/
    └── zip/
        ├── darwin/arm64/
        │   └── NERIS Sublingual-darwin-arm64-1.0.0.zip
        └── win32/x64/
            └── NERIS Sublingual-win32-x64-1.0.0.zip
```

---

# Đổi Logo

## File hiện tại

| File | Định dạng | Vị trí | Cách tạo |
|------|-----------|--------|----------|
| `assets/logo.svg` | SVG vector | `desktop/assets/` | Thiết kế bằng tay hoặc tool (Figma, Illustrator, code) |
| `assets/logo.png` | PNG 1024×1024 | `desktop/assets/` | Render từ SVG |
| `assets/logo.icns` | macOS icon | `desktop/assets/` | Convert từ PNG bằng `iconutil` |
| `assets/logo.ico` | Windows icon | `desktop/assets/` | Convert từ PNG bằng `magick` |

Logo hiện tại: **Ocean Monogram "N"** — đường thẳng (cấu trúc) + đường cong (dòng chảy đại dương), màu sắc từ palette NERIS.

## Cách đổi

Có 2 cách: **từ SVG** (khuyên dùng) hoặc **từ PNG**.

### Cách A: Từ file SVG (khuyên dùng)

Chỉnh sửa file `assets/logo.svg`, sau đó render ra các format:

```bash
cd desktop

# Bước 1: Render SVG → PNG 1024×1024
# Cách 1: Dùng rsvg-convert (cài: brew install librsvg)
rsvg-convert -w 1024 -h 1024 assets/logo.svg > /tmp/logo.png

# Cách 2: Dùng qlmanage (built-in macOS)
qlmanage -t -s 1024 -o /tmp assets/logo.svg
mv /tmp/logo.svg.png /tmp/logo.png

# Bước 2: Copy PNG
cp /tmp/logo.png assets/logo.png

# Bước 3: Tạo ICNS cho macOS
mkdir -p /tmp/icon.iconset
for size in 16 32 128 256 512; do
  sips -z $size $size /tmp/logo.png --out /tmp/icon.iconset/icon_${size}x${size}.png
done
for size in 32 64 256 512 1024; do
  half=$((size/2))
  sips -z $size $size /tmp/logo.png --out /tmp/icon.iconset/icon_${half}x${half}@2x.png
done
iconutil -c icns /tmp/icon.iconset -o assets/logo.icns
rm -rf /tmp/icon.iconset

# Bước 4: Tạo ICO cho Windows (cần: brew install imagemagick)
magick /tmp/logo.png -define icon:auto-resize=256,128,64,48,32,16 assets/logo.ico

# Bước 5: Build lại
rm -rf .vite/ out/
pnpm make:mac
pnpm make:win
```

### Cách B: Từ file PNG

### Bước 2: Tạo ICNS cho macOS

```bash
# Tạo thư mục tạm chứa các kích thước
mkdir -p icon.iconset

# Resize ra các kích thước cần thiết
sips -z 16 16   icon.png --out icon.iconset/icon_16x16.png
sips -z 32 32   icon.png --out icon.iconset/icon_16x16@2x.png
sips -z 32 32   icon.png --out icon.iconset/icon_32x32.png
sips -z 64 64   icon.png --out icon.iconset/icon_32x32@2x.png
sips -z 128 128 icon.png --out icon.iconset/icon_128x128.png
sips -z 256 256 icon.png --out icon.iconset/icon_128x128@2x.png
sips -z 256 256 icon.png --out icon.iconset/icon_256x256.png
sips -z 512 512 icon.png --out icon.iconset/icon_256x256@2x.png
sips -z 512 512 icon.png --out icon.iconset/icon_512x512.png
sips -z 1024 1024 icon.png --out icon.iconset/icon_512x512@2x.png

# Tạo .icns từ iconset
iconutil -c icns icon.iconset -o assets/logo.icns

# Dọn dẹp
rm -rf icon.iconset
```

### Bước 3: Tạo ICO cho Windows

```bash
# Dùng sips resize + ImageMagick convert (cài qua brew install imagemagick)
sips -z 256 256 icon.png --out /tmp/icon-256.png
convert /tmp/icon-256.png -define icon:auto-resize=256,128,64,48,32,16 assets/logo.ico
rm /tmp/icon-256.png
```

### Bước 4: Copy PNG fallback

```bash
cp icon.png assets/logo.png
```

### Bước 5: Build lại

Xoá cache build cũ và build lại:

```bash
rm -rf .vite/ out/
pnpm make:mac
pnpm make:win
```

---

# Signing & Notarization (macOS)

## Không có Developer ID (mặc định)

App chạy được nhưng người dùng phải **Ctrl+Click → Open** lần đầu để bypass Gatekeeper.

## Có Developer ID (Apple Developer Program — $99/year)

Thêm vào `forge.config.ts` trong `packagerConfig`:

```ts
osxSign: {
  identity: 'Developer ID Application: Your Name (TEAMID)',
},
osxNotarize: {
  appleApiKey: process.env.APPLE_API_KEY_ID,
  appleApiKeyId: process.env.APPLE_API_KEY_ID,
  appleApiIssuer: process.env.APPLE_API_KEY_ISSUER,
},
```

Tạo API Key từ [App Store Connect](https://appstoreconnect.apple.com/access/integrations/api) và set environment variables trước khi build:

```bash
export APPLE_API_KEY_ID="key_id"
export APPLE_API_KEY_ISSUER="uuid"
export APPLE_API_KEY="/path/to/AuthKey_XXXXXXXXXX.p8"
```

---

# Cấu hình liên quan

| File | Mục đích |
|------|----------|
| `forge.config.ts` | Electron Forge config (makers, packagerConfig, plugins) |
| `package.json` | App metadata (name, version, productName, scripts) |
| `vite.main.config.ts` | Vite config cho main process (external: koffi, adm-zip) |
| `vite.renderer.config.ts` | Vite config cho renderer (React, Tailwind, multi-page) |
| `vite.preload.config.ts` | Vite config cho preload scripts (external: electron) |

## Native dependencies cần chú ý

| Library | Vị trí trong bundle | File nguồn |
|---------|-------------------|------------|
| libvosk (Vosk STT) | `Resources/bin/vosk/` | `vosk-bindings.ts`, `vosk-worker.ts` |
| libScreenCaptureKitBridge | `Resources/native/screencapture-mac/` | `screencapture-mac.ts` |

Cả 2 đều dùng `process.resourcesPath` để resolve path khi app đã packaged. Khi dev, dùng relative path từ project root.

---

# Troubleshooting

## App không mở sau build

1. **Kiểm tra log**: Mở Terminal, chạy trực tiếp binary:
   ```bash
   # Chạy app và ghi log ra file
   out/NERIS\ Sublingual-darwin-arm64/NERIS\ Sublingual.app/Contents/MacOS/neris-sublingual --enable-logging 2>/tmp/app.log

   # Xem tất cả lỗi console từ renderer
   grep "CONSOLE" /tmp/app.log

   # Xem tất cả lỗi
   grep -i "error\|fail\|cannot\|denied" /tmp/app.log
   ```
2. **Lỗi dylib**: Kiểm tra native libs có trong Resources không:
   ```bash
   ls out/NERIS\ Sublingual-darwin-arm64/NERIS\ Sublingual.app/Contents/Resources/native/screencapture-mac/
   ls out/NERIS\ Sublingual-darwin-arm64/NERIS\ Sublingual.app/Contents/Resources/bin/vosk/
   ```

## App chạy nhưng màn hình đen/trắng (renderer không load)

Nguyên nhân: `BrowserRouter` không hoạt động với `file://` protocol trong packaged app. Khi `loadFile()` tải trang, React Router không match được route nào → trang trống.

Fix: dùng `HashRouter` trong `src/App.tsx`:
```tsx
import { HashRouter } from "react-router-dom";
// Thay BrowserRouter → HashRouter
<HashRouter>...</HashRouter>
```

HashRouter dùng URL hash (`#/`, `#/settings`) hoạt động với mọi protocol.

## Cannot find module 'koffi'

pnpm + Electron Forge không tự động include `node_modules` vào asar. Đã fix trong `forge.config.ts`:
- `prune: false` — tắt npm prune (không hoạt động với pnpm)
- `ignore` function — chỉ giữ `koffi`, `adm-zip`, `@koromix`, exclude phần còn lại
- `asar.unpackDir: 'node_modules/**/*.node'` — extract native `.node` binaries khỏi asar
- `OnlyLoadAppFromAsar: false` — cho phép load native module từ ngoài asar

## Windows build trên macOS bị lỗi

Build ZIP không cần Wine. Nếu có lỗi, đảm bảo `MakerSquirrel` đã bị xoá khỏi `forge.config.ts`.

## Icon không hiển thị

- macOS: icon phải là `.icns` format. Dùng `iconutil -c icns` để tạo.
- Windows: icon phải là `.ico` format với nhiều resolution (16→256).
- Xoá cache: `rm -rf .vite/ out/` rồi build lại.
