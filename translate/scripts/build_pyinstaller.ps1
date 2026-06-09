<#
.SYNOPSIS
    Build translate-service.exe from Python app using PyInstaller.
.DESCRIPTION
    Output goes to ../desktop/bin/translate/
.PARAMETER clean
    Remove build/ and dist/ before building.
#>
param([switch]$clean)

$ErrorActionPreference = "Stop"
$os = if ($IsWindows) { "Windows" } elseif ($IsMacOS) { "macOS" } elseif ($IsLinux) { "Linux" } else { "Unknown" }

trap {
    Write-Error "::error:: [OS: $os] Build failed: $_"
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Resolve-Path "$scriptDir/.."
$outputDir = Resolve-Path "$scriptDir/../../desktop/bin/translate" -ErrorAction SilentlyContinue

if (-not $outputDir) {
    New-Item -ItemType Directory -Path "$scriptDir/../../desktop/bin/translate" -Force | Out-Null
    $outputDir = Resolve-Path "$scriptDir/../../desktop/bin/translate"
}

Write-Host "Building translate-service.exe..."
Write-Host "  OS:      $os"
Write-Host "  Project: $projectDir"
Write-Host "  Output:  $outputDir"

if ($clean) {
    $buildDir = Join-Path $projectDir "build"
    $distDir  = Join-Path $projectDir "dist"
    if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
    if (Test-Path $distDir)  { Remove-Item -Recurse -Force $distDir }
    Write-Host "  Cleaned build/dist directories"
}

# Fix: Windows Python may not have 'python3' alias, use 'python'.
# ensurepip can fail on some Python builds, so use --without-pip + get-pip.py.
if (-not (Test-Path "$projectDir/.venv")) {
    Write-Host "  Creating virtual environment..."
    python -m venv --without-pip "$projectDir/.venv"
    $getPip = "$projectDir/.venv/get-pip.py"
    Invoke-WebRequest -Uri https://bootstrap.pypa.io/get-pip.py -OutFile $getPip
    & "$projectDir/.venv/Scripts/python.exe" $getPip
    Remove-Item $getPip
}

& "$projectDir/.venv/Scripts/pip.exe" install -r "$projectDir/requirements.txt"
if ($LASTEXITCODE -ne 0) { throw "pip install -r requirements.txt failed" }

& "$projectDir/.venv/Scripts/pip.exe" install pyinstaller
if ($LASTEXITCODE -ne 0) { throw "pip install pyinstaller failed" }

Push-Location $projectDir

try {
    & "$projectDir/.venv/Scripts/pyinstaller.exe" `
        --onefile `
        --name translate-service `
        --distpath $outputDir `
        --workpath "$projectDir/build" `
        --specpath "$projectDir/build" `
        --add-data "$projectDir\.env.example;." `
        --copy-metadata tqdm `
        --copy-metadata huggingface-hub `
        --copy-metadata tokenizers `
        --copy-metadata transformers `
        --hidden-import "app.translator" `
        --hidden-import "app.translator.nllb_ct2" `
        --hidden-import "app.translator.model_manager" `
        --hidden-import "app.translator.session_cache" `
        --hidden-import "app.postprocess" `
        --hidden-import "app.postprocess.vi_normalizer" `
        --hidden-import "app.postprocess.glossary" `
        --hidden-import "app.utils" `
        --hidden-import "app.utils.text" `
        --hidden-import "app.utils.logger" `
        --hidden-import "ctranslate2" `
        --hidden-import "transformers" `
        --hidden-import "sentencepiece" `
        app/main.py

    if ($LASTEXITCODE -ne 0) { throw "PyInstaller build failed" }
} finally {
    Pop-Location
}

Write-Host "Done: $outputDir/translate-service.exe"
