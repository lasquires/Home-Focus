$ErrorActionPreference = "SilentlyContinue"
# Administrator-only recovery if the agent/update ever fails while Windows Firewall is fail-closed.
Get-NetFirewallRule -Group "HomeFocus" | Remove-NetFirewallRule
$backup = Join-Path $env:ProgramData "HomeFocus\firewall-backup.json"
if(Test-Path $backup){
  $items = Get-Content -Raw $backup | ConvertFrom-Json
  foreach($item in @($items)){ Set-NetFirewallProfile -Name $item.Name -DefaultOutboundAction $item.DefaultOutboundAction }
}else{
  Set-NetFirewallProfile -Profile Domain,Private,Public -DefaultOutboundAction Allow
}
# Disable the current administrator's WinINET proxy. Run this once in the affected child account too if necessary.
$key="HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"
Set-ItemProperty $key ProxyEnable 0
Write-Host "Emergency unlock applied."
