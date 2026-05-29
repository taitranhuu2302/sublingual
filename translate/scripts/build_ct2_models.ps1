$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$ProjectVenvPython = Join-Path $ProjectDir "venv/Scripts/python.exe"

$PythonBin = if ($env:PYTHON_BIN) {
    $env:PYTHON_BIN
} elseif (Test-Path $ProjectVenvPython) {
    $ProjectVenvPython
} else {
    "python"
}

$Quantization = "int8"
$QuantizationSet = $false
$Force = $false

foreach ($arg in $args) {
  if ($arg -eq "--force") {
    $Force = $true
  } elseif (-not $QuantizationSet -and $arg) {
    $Quantization = $arg
    $QuantizationSet = $true
  }
}

$ForceFlag = if ($Force) { "--force" } else { $null }

try {
    & $PythonBin -c "import transformers" | Out-Null
} catch {
    Write-Error "transformers is not installed for $PythonBin. Install dependencies in the project environment or set PYTHON_BIN explicitly."
    exit 1
}

& $PythonBin "$ProjectDir/scripts/convert_marian_to_ct2.py" `
  --hf_model Helsinki-NLP/opus-mt-en-vi `
  --output_dir "$ProjectDir/models/ct2/en-vi" `
  --quantization $Quantization `
  $ForceFlag

& $PythonBin "$ProjectDir/scripts/convert_marian_to_ct2.py" `
  --hf_model Helsinki-NLP/opus-mt-vi-en `
  --output_dir "$ProjectDir/models/ct2/vi-en" `
  --quantization $Quantization `
  $ForceFlag

& $PythonBin "$ProjectDir/scripts/convert_marian_to_ct2.py" `
  --hf_model Helsinki-NLP/opus-mt-zh-vi `
  --output_dir "$ProjectDir/models/ct2/zh-vi" `
  --quantization $Quantization `
  $ForceFlag

Write-Host "CT2 models created successfully with quantization=$Quantization"
