param(
    [string]$Venv = ".venv"
)

$ErrorActionPreference = "Stop"

Write-Host "[YourPark VC PoC] Creating Python 3.10 venv..."
if (-not (Test-Path $Venv)) {
    py -3.10 -m venv $Venv
}

$Python = Join-Path $Venv "Scripts/python.exe"

& $Python -m pip install --upgrade pip setuptools wheel

Write-Host "[YourPark VC PoC] Installing CUDA PyTorch 2.6.0..."
& $Python -m pip install torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cu124

Write-Host "[YourPark VC PoC] Installing Chatterbox..."
& $Python -m pip install chatterbox-tts==0.1.7

Write-Host ""
Write-Host "Setup complete."
Write-Host "Run:"
Write-Host "  $Python vc_test.py --source source.wav --target target.wav --output converted.wav --reference-seconds 2 5 10"