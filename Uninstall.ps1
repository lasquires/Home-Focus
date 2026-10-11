param([switch]$KeepFiles)
$ErrorActionPreference = "Stop"
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Run Uninstall.ps1 as Administrator." }
& "$PSScriptRoot\Emergency-Unlock.ps1"
schtasks /Delete /TN "HomeFocus Agent" /F 2>$null | Out-Null
Remove-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name HomeFocus -ErrorAction SilentlyContinue
Get-Process HomeFocus,HomeFocus.Agent -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (-not $KeepFiles) { Remove-Item (Join-Path $env:ProgramFiles "HomeFocus") -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "HomeFocus removed. ProgramData policy/history was left in place intentionally."
