param(
  [string]$DistPath = "$PSScriptRoot\dist",
  [switch]$NoStart
)
$ErrorActionPreference = "Stop"
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Run Install.ps1 as Administrator." }
$install = Join-Path $env:ProgramFiles "HomeFocus"
$data = Join-Path $env:ProgramData "HomeFocus"
$required = @("HomeFocus.exe","HomeFocus.Agent.exe","HomeFocus.Updater.exe")
foreach($f in $required){ if(!(Test-Path (Join-Path $DistPath $f))){ throw "Missing $f. Run tools\Build-Release.ps1 first." } }

New-Item -ItemType Directory -Force -Path $install,$data | Out-Null
try { schtasks /End /TN "HomeFocus Agent" 2>$null | Out-Null } catch {}
Get-Process HomeFocus,HomeFocus.Agent -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
# The installer has already put its self-contained binaries into Program Files.
# Keep manual installs working with an external dist folder.
if (-not [string]::Equals(
  [IO.Path]::GetFullPath($DistPath).TrimEnd('\'),
  [IO.Path]::GetFullPath($install).TrimEnd('\'),
  [StringComparison]::OrdinalIgnoreCase
)) {
  Copy-Item (Join-Path $DistPath '*') $install -Force
}

# ProgramData policy is writable only by SYSTEM/Administrators; the child UI talks to the agent over loopback.
icacls $data /inheritance:r /grant:r "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null

$agent = Join-Path $install "HomeFocus.Agent.exe"
schtasks /Create /TN "HomeFocus Agent" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR "`"$agent`"" /F | Out-Null
schtasks /Run /TN "HomeFocus Agent" | Out-Null

# Start the child-facing UI at every interactive logon. It configures the current user's browser proxy to the local agent.
$run = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
New-ItemProperty -Path $run -Name "HomeFocus" -Value "`"$(Join-Path $install 'HomeFocus.exe')`"" -PropertyType String -Force | Out-Null
if (-not $NoStart) { Start-Process (Join-Path $install "HomeFocus.exe") }
Write-Host "HomeFocus installed. Reboot once to verify boot-time enforcement."
