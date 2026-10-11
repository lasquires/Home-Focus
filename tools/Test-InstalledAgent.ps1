param([string]$Version = "1.0.2")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root "HomeFocus-Setup-$Version.exe"
if (-not (Test-Path $setup)) { throw "Installer missing: $setup" }
$setupLog = Join-Path $env:RUNNER_TEMP "HomeFocus-setup-test.log"
try {
  Write-Host "Installing HomeFocus $Version in CI to test agent startup..."
  $p = Start-Process -FilePath $setup -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=$setupLog" -PassThru -Wait
  if ($p.ExitCode -ne 0) { throw "Setup returned exit code $($p.ExitCode)." }

  $task = Get-ScheduledTask -TaskName "HomeFocus Agent" -ErrorAction Stop
  Write-Host "Task state: $($task.State)"
  $configPath = Join-Path $env:ProgramData "HomeFocus\config.json"
  if (-not (Test-Path $configPath)) { throw "Agent did not create config.json." }
  $cfg = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
  if ($cfg.Enabled) { throw "New installations must not automatically enable machine-wide filtering." }

  $connected = $false
  for ($i=0; $i -lt 30; $i++) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
      $client.Connect("127.0.0.1",48191)
      $client.ReceiveTimeout = 3000
      $stream = $client.GetStream()
      $writer = [System.IO.StreamWriter]::new($stream)
      $writer.AutoFlush = $true
      $writer.WriteLine('{"Type":"status"}')
      $reader = [System.IO.StreamReader]::new($stream)
      $json = $reader.ReadLine()
      if (-not $json) { throw "Empty response from agent." }
      $response = $json | ConvertFrom-Json
      if (-not $response.Ok) { throw "Agent replied with an error: $json" }
      if ($response.Status.PinConfigured) { throw "New installation has unexpected parent PIN." }
      if ($response.Status.Enabled) { throw "Agent unexpectedly enabled filtering." }
      $connected = $true
      Write-Host "Agent responded successfully. Parent PIN is not yet set, and filtering is disabled."
      break
    } catch {
      if (($i % 5) -eq 0) { Write-Warning ("RPC attempt $($i + 1): " + $_.Exception.Message) }
      Start-Sleep -Seconds 1
    }
    finally { $client.Close() }
  }
  if (-not $connected) { throw "Installed agent never answered a localhost status request." }
}
catch {
  Write-Host "== Setup log =="
  if (Test-Path $setupLog) { Get-Content $setupLog -Tail 80 }
  Write-Host "== Installer script log =="
  $installLog = Join-Path $env:ProgramData "HomeFocus-install.log"
  if (Test-Path $installLog) { Get-Content $installLog -Tail 80 }
  Write-Host "== Agent log =="
  $agentLog = Join-Path $env:ProgramData "HomeFocus\HomeFocus.log"
  if (Test-Path $agentLog) { Get-Content $agentLog -Tail 80 }
  Write-Host "== Scheduled task =="
  & schtasks.exe /Query /TN "HomeFocus Agent" /V /FO LIST
  throw
}
