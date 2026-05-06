$ErrorActionPreference = 'Stop'

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendDir = Join-Path $RootDir 'backend'
$VenvDir = Join-Path $BackendDir '.venv'
$VenvPython = Join-Path $VenvDir 'Scripts\python.exe'

Write-Host 'Installing Node dependencies with pnpm...'
Set-Location $RootDir
pnpm install

Write-Host 'Setting up Python virtual environment in backend\.venv...'
Set-Location $BackendDir
if (-not (Test-Path $VenvDir)) {
  python -m venv .venv
}

if (Test-Path $VenvPython) {
  $PythonBin = $VenvPython
} else {
  $PythonBin = 'python'
}

Write-Host 'Installing backend Python dependencies...'
& $PythonBin -m pip install --upgrade pip
& $PythonBin -m pip install -r requirements.txt

Write-Host 'Done.'
Write-Host 'Run backend with: pnpm backend'

