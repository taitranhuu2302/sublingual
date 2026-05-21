# Known Issue: macOS startup crash in libclrjit

## Symptom

On some machines (observed on Apple Silicon + macOS 26.1), the packaged app crashes shortly after launch with:

- `EXC_CRASH (SIGABRT)`
- `abort() called`
- stack frames inside `.NET` JIT runtime (`libclrjit.dylib`, `libcoreclr.dylib`)
- crash frequently appears on `RenderTimerLoop` while runtime is compiling methods

## Observed evidence from crash reports

- Crashes happen before user interaction with capture features.
- Faulting frames are inside JIT internals such as `jitNativeCode(...)` and `Compiler::...`.
- Native app dependencies (`libAvaloniaNative.dylib`, `libSkiaSharp.dylib`, macOS bridge dylib) are loaded, but the termination path points to managed runtime abort.

## Root-cause assessment

Current assessment is:

1. **Primary cause is a runtime/JIT instability** in the packaged `.NET 10` macOS self-contained runtime path on the observed OS/runtime combination.
2. This is **not primarily an application business-logic crash** (not capture flow, not translation flow, not overlay state).
3. This is also **not the typical missing-file issue** (hostfxr/coreclr and native Avalonia libraries are present and loadable).

## Why `System.Runtime.TieredCompilation=false` was added

Disabling tiered compilation reduces background JIT activity and is a common mitigation for JIT regressions.

However, if crash still occurs after confirming runtimeconfig contains:

```json
"System.Runtime.TieredCompilation": false
```

then the issue is likely broader than tiered compilation alone (still JIT/runtime related).

## Recommended mitigations

### 1) Preferred release mitigation

- Ship macOS release using `net9.0` (LTS) instead of `net10.0` until the runtime issue is resolved upstream.

### 2) Additional runtime switches for validation

When testing locally, try disabling more JIT features:

- `COMPlus_TieredCompilation=0`
- `COMPlus_TieredPGO=0`
- `COMPlus_ReadyToRun=0`

If these reduce or remove startup crashes, it further confirms JIT/runtime-level instability.

### 3) Packaging strategy fallback

- Prefer framework-dependent publish for internal testing to compare behavior against self-contained runtime.
- Keep self-contained packaging for distribution only after stability is verified.

## Scope impact

- Affects startup reliability of packaged macOS builds.
- Not directly tied to capture model selection, translation settings, or overlay reset logic.

## Tracking note

When collecting future crash reports, include:

- `Sublingual.App.runtimeconfig.json`
- target framework (`net10.0` or `net9.0`)
- publish mode (self-contained vs framework-dependent)
- macOS version and CPU architecture

This data is enough to quickly confirm whether it is the same JIT-runtime class of failure.
