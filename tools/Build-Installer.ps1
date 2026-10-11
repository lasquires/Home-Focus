param([string]$Version = "1.0.1")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$compiler = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) { throw "Inno Setup 6 compiler ISCC.exe is required." }
foreach ($name in @("HomeFocus.exe","HomeFocus.Agent.exe","HomeFocus.Updater.exe")) {
  if (-not (Test-Path (Join-Path $root "dist\$name"))) { throw "Missing dist\$name. Run tools\Build-Release.ps1 first." }
}
& $compiler "/DAppVersion=$Version" "/DSourceRoot=$root" "/DOutputDir=$root" (Join-Path $root "installer\HomeFocus.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with code $LASTEXITCODE." }
$path = Join-Path $root "HomeFocus-Setup-$Version.exe"
if (-not (Test-Path $path)) { throw "Installer was not created: $path" }
Write-Host "Created $path"
