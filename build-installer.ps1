$ErrorActionPreference = "Stop"

$tempBuildRoot = Join-Path $env:LOCALAPPDATA "Temp\VolumePieBuild"
$tempPublish = Join-Path $tempBuildRoot "publish"
$tempDist = Join-Path $tempBuildRoot "dist"

New-Item -ItemType Directory -Force -Path $tempPublish | Out-Null
New-Item -ItemType Directory -Force -Path $tempDist | Out-Null

Write-Host "=== 1. Building and publishing VolumePie ===" -ForegroundColor Cyan
Get-Process VolumePie -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300
if (Test-Path "src\VolumePie\obj") {
    Remove-Item -Recurse -Force "src\VolumePie\obj" -ErrorAction SilentlyContinue
}

dotnet publish "src\VolumePie\VolumePie.csproj" -c Release -r win-x64 --self-contained false -o $tempPublish
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

New-Item -ItemType Directory -Force -Path "publish" | Out-Null
Copy-Item -Path "$tempPublish\*" -Destination "publish" -Recurse -Force

$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Error "Inno Setup 6 (ISCC.exe) not found."
    exit 1
}

# Ensure VolumePie.iss has UTF-8 BOM for Inno Setup Japanese text support
$issPath = Join-Path (Get-Location) "installer\VolumePie.iss"
$issBytes = [System.IO.File]::ReadAllBytes($issPath)
if ($issBytes.Length -ge 3 -and -not ($issBytes[0] -eq 0xEF -and $issBytes[1] -eq 0xBB -and $issBytes[2] -eq 0xBF)) {
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    $issText = [System.Text.Encoding]::UTF8.GetString($issBytes)
    [System.IO.File]::WriteAllText($issPath, $issText, $utf8Bom)
}

Write-Host "=== 2. Compiling Inno Setup Installer ===" -ForegroundColor Cyan
& $iscc "/O$tempDist" $issPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
    exit 1
}

New-Item -ItemType Directory -Force -Path "dist" | Out-Null
Copy-Item -Path "$tempDist\*" -Destination "dist" -Force

Write-Host "=== Done! Installer created in .\dist\VolumePie-Setup-1.0.2.exe ===" -ForegroundColor Green
