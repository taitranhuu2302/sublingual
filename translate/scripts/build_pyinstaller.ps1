<#
.SYNOPSIS
    Build translate-service.exe from Python app using PyInstaller.
.DESCRIPTION
    Output goes to ../desktop/bin/translate/
.PARAMETER clean
    Remove build/ and dist/ before building.
#>
param([switch]$clean)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Resolve-Path "$scriptDir/.."
$outputDir = Resolve-Path "$scriptDir/../../desktop/bin/translate" -ErrorAction SilentlyContinue

if (-not $outputDir) {
    New-Item -ItemType Directory -Path "$scriptDir/../../desktop/bin/translate" -Force | Out-Null
    $outputDir = Resolve-Path "$scriptDir/../../desktop/bin/translate"
}

Write-Host "Building translate-service.exe..."
Write-Host "  Project: $projectDir"
Write-Host "  Output:  $outputDir"

if ($clean) {
    $buildDir = Join-Path $projectDir "build"
    $distDir  = Join-Path $projectDir "dist"
    if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
    if (Test-Path $distDir)  { Remove-Item -Recurse -Force $distDir }
    Write-Host "  Cleaned build/dist directories"
}

& "$projectDir/.venv/Scripts/pip.exe" install pyinstaller
if ($LASTEXITCODE -ne 0) { throw "Failed to install pyinstaller" }

Push-Location $projectDir

try {
    & "$projectDir/.venv/Scripts/pyinstaller.exe" `
        --onefile `
        --name translate-service `
        --distpath $outputDir `
        --workpath "$projectDir/build" `
        --specpath "$projectDir/build" `
        --add-data ".env.example;." `
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
