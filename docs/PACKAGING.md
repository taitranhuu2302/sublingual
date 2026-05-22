# Packaging

This repository currently ships minimal packaging flows for macOS and Windows that produce self-contained `dotnet publish` outputs and zipped release artifacts.

## Scope

Current packaging support:

- macOS
- Windows
- native `ScreenCaptureKit` bridge build included for macOS
- self-contained .NET runtime included
- distributable `.zip` artifact
- macOS `.app` bundle artifact

Not included yet:

- code signing
- notarization
- Windows installer or `.msi`

## Output

Running the packaging scripts creates:

- `artifacts/macos/<rid>/publish/`
- `artifacts/macos/<rid>/sublingual-<rid>.zip`
- `artifacts/macos/<rid>/Sublingual.app`
- `artifacts/windows/<rid>/publish/`
- `artifacts/windows/<rid>/sublingual-<rid>.zip`

Examples:

- `artifacts/macos/osx-arm64/publish/`
- `artifacts/macos/osx-arm64/sublingual-osx-arm64.zip`
- `artifacts/macos/osx-arm64/Sublingual.app`
- `artifacts/windows/win-x64/publish/`
- `artifacts/windows/win-x64/sublingual-win-x64.zip`

## Prerequisites

- macOS
- `.NET SDK 10`
- `clang++`
- `ScreenCaptureKit` available in the local SDK

## Package For Current Mac

```bash
bash ./scripts/package-macos.sh
```

This produces the zipped publish output.

The script auto-detects the runtime identifier:

- Apple Silicon -> `osx-arm64`
- Intel Mac -> `osx-x64`

## Package For A Specific Runtime

```bash
bash ./scripts/package-macos.sh osx-arm64
bash ./scripts/package-macos.sh osx-x64
```

## Build A macOS .app Bundle

```bash
bash ./scripts/package-macos-app.sh
```

Default runtime:

- Apple Silicon -> `osx-arm64`
- Intel Mac -> `osx-x64`

You can also override the bundle identifier and version:

```bash
bash ./scripts/package-macos-app.sh osx-arm64 com.sublingual.app 0.1.0
```

## Sign A macOS .app Bundle

After building `Sublingual.app`, sign it with your Developer ID Application identity:

```bash
bash ./scripts/sign-macos-app.sh osx-arm64 "Developer ID Application: Your Name (TEAMID)"
```

The script:

- signs `artifacts/macos/<rid>/Sublingual.app`
- uses `packaging/macos/entitlements.plist`
- verifies the result with `codesign --verify`
- runs `spctl --assess`

## Package For Windows On Windows

```powershell
pwsh ./scripts/package-windows.ps1
```

Default runtime:

- `win-x64`

You can also publish for another Windows runtime identifier:

```powershell
pwsh ./scripts/package-windows.ps1 win-x64
pwsh ./scripts/package-windows.ps1 win-arm64
```

## Cross-Publish For Windows From Bash

If you are packaging a Windows zip from a Unix-like shell, keep using the existing bash script:

```bash
bash ./scripts/package-windows.sh
```

## What The Script Does

macOS script:

1. builds the native macOS bridge from `native/macos/ScreenCaptureKitBridge/`
2. runs `dotnet publish` for `src/Sublingual.App/Sublingual.App.csproj`
3. outputs a self-contained Release publish folder
4. zips the publish folder for distribution

macOS app script:

1. builds the native macOS bridge from `native/macos/ScreenCaptureKitBridge/`
2. runs `dotnet publish` for `src/Sublingual.App/Sublingual.App.csproj`
3. creates `Sublingual.app/Contents/MacOS` from publish output
4. writes `Contents/Info.plist`
5. copies `libScreenCaptureKitBridge.dylib` into `Contents/Resources/native/`

macOS signing script:

1. signs `Sublingual.app` with a Developer ID Application identity
2. applies hardened runtime with `packaging/macos/entitlements.plist`
3. verifies the signature locally

Windows script:

1. runs `dotnet publish` for `src/Sublingual.App/Sublingual.App.csproj`
2. outputs a self-contained Release publish folder
3. zips the publish folder for distribution

## Manual Equivalent

```bash
bash ./scripts/build-macos-native.sh
dotnet publish "src/Sublingual.App/Sublingual.App.csproj" -c Release -r osx-arm64 --self-contained true -o "artifacts/macos/osx-arm64/publish"
ditto -c -k --sequesterRsrc --keepParent "artifacts/macos/osx-arm64/publish" "artifacts/macos/osx-arm64/sublingual-osx-arm64.zip"
```

```bash
bash ./scripts/build-macos-native.sh
dotnet publish "src/Sublingual.App/Sublingual.App.csproj" -c Release -r osx-arm64 --self-contained true -o "artifacts/macos/osx-arm64/publish"
mkdir -p "artifacts/macos/osx-arm64/Sublingual.app/Contents/MacOS" "artifacts/macos/osx-arm64/Sublingual.app/Contents/Resources/native"
cp -R "artifacts/macos/osx-arm64/publish"/. "artifacts/macos/osx-arm64/Sublingual.app/Contents/MacOS/"
cp "native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib" "artifacts/macos/osx-arm64/Sublingual.app/Contents/Resources/native/libScreenCaptureKitBridge.dylib"
```

```bash
dotnet publish "src/Sublingual.App/Sublingual.App.csproj" -c Release -r win-x64 --self-contained true -o "artifacts/windows/win-x64/publish"
pwsh -Command "Compress-Archive -LiteralPath 'artifacts/windows/win-x64/publish' -DestinationPath 'artifacts/windows/win-x64/sublingual-win-x64.zip'"
```

## Run The Published App Locally

From the publish folder:

```bash
./Sublingual.App
```

From the app bundle:

```bash
open "artifacts/macos/osx-arm64/Sublingual.app"
```

## Notes

- The native library `libScreenCaptureKitBridge.dylib` is copied into publish output through the existing project file configuration.
- The app bundle script also copies `libScreenCaptureKitBridge.dylib` into `Sublingual.app/Contents/Resources/native/` so the macOS app can resolve it when launched outside the repo root.
- macOS publish disables tiered compilation through runtime configuration because the self-contained `net10.0` package has shown `libclrjit` startup crashes on macOS 26 during background JIT compilation.
- If Gatekeeper blocks execution on another machine, you will need a later signing/notarization step.
- `packaging/macos/Info.plist.template` is used to generate the app bundle metadata.
- `packaging/macos/entitlements.plist` is used by `scripts/sign-macos-app.sh` for hardened runtime signing.
- The signing script does not notarize the app. Notarization is still a separate later step.
- The Windows flow currently produces a zipped publish output, not an installer.
- `scripts/package-windows.ps1` is the native Windows packaging entry point; `scripts/package-windows.sh` remains available for cross-publishing from bash environments.
- Cross-publishing a Windows build from macOS is supported by the bash script, but final runtime validation should still be done on a Windows machine.
