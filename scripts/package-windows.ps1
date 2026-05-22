param(
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RootDir "src/Sublingual.App/Sublingual.App.csproj"
$ArtifactsDir = Join-Path $RootDir "artifacts/windows/$RuntimeIdentifier"
$PublishDir = Join-Path $ArtifactsDir "publish"
$ZipPath = Join-Path $ArtifactsDir "sublingual-$RuntimeIdentifier.zip"

if (Test-Path -LiteralPath $ArtifactsDir) {
    Remove-Item -LiteralPath $ArtifactsDir -Recurse -Force
}

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

dotnet publish $AppProject `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    -o $PublishDir

if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

Compress-Archive -LiteralPath $PublishDir -DestinationPath $ZipPath

"Created Windows package:`n- publish: $PublishDir`n- zip: $ZipPath"
