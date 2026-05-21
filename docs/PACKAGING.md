# Packaging

This repository currently ships minimal packaging flows for macOS and Windows that produce self-contained `dotnet publish` outputs and zipped release artifacts.

## Scope

Current packaging support:

- macOS only
- Windows
- native `ScreenCaptureKit` bridge build included for macOS
- self-contained .NET runtime included
- distributable `.zip` artifact

Not included yet:

- `.app` bundle generation
- code signing
- notarization
- Windows installer or `.msi`

## Output

Running the packaging scripts creates:

- `artifacts/macos/<rid>/publish/`
- `artifacts/macos/<rid>/sublingual-<rid>.zip`
- `artifacts/windows/<rid>/publish/`
- `artifacts/windows/<rid>/sublingual-<rid>.zip`

Examples:

- `artifacts/macos/osx-arm64/publish/`
- `artifacts/macos/osx-arm64/sublingual-osx-arm64.zip`
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

The script auto-detects the runtime identifier:

- Apple Silicon -> `osx-arm64`
- Intel Mac -> `osx-x64`

## Package For A Specific Runtime

```bash
bash ./scripts/package-macos.sh osx-arm64
bash ./scripts/package-macos.sh osx-x64
```

## Package For Windows

```bash
bash ./scripts/package-windows.sh
```

Default runtime:

- `win-x64`

You can also publish for another Windows runtime identifier:

```bash
bash ./scripts/package-windows.sh win-x64
bash ./scripts/package-windows.sh win-arm64
```

## What The Script Does

macOS script:

1. builds the native macOS bridge from `native/macos/ScreenCaptureKitBridge/`
2. runs `dotnet publish` for `src/Sublingual.App/Sublingual.App.csproj`
3. outputs a self-contained Release publish folder
4. zips the publish folder for distribution

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
dotnet publish "src/Sublingual.App/Sublingual.App.csproj" -c Release -r win-x64 --self-contained true -o "artifacts/windows/win-x64/publish"
ditto -c -k --sequesterRsrc --keepParent "artifacts/windows/win-x64/publish" "artifacts/windows/win-x64/sublingual-win-x64.zip"
```

## Run The Published App Locally

From the publish folder:

```bash
./Sublingual.App
```

## Notes

- The native library `libScreenCaptureKitBridge.dylib` is copied into publish output through the existing project file configuration.
- macOS publish disables tiered compilation through runtime configuration because the self-contained `net10.0` package has shown `libclrjit` startup crashes on macOS 26 during background JIT compilation.
- If Gatekeeper blocks execution on another machine, you will need a later signing/notarization step.
- If you want a double-clickable `.app`, add a dedicated macOS app-bundle packaging step after this publish flow is stable.
- The Windows flow currently produces a zipped publish output, not an installer.
- Cross-publishing a Windows build from macOS is supported by the script, but final runtime validation should still be done on a Windows machine.
