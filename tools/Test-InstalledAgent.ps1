param([string]$Version = "1.0.3")
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
      $scriptureTask = $response.Status.Tasks | Where-Object { $_.Task.Id -eq "typing-youtube-20" } | Select-Object -First 1
      $scripture = $response.Status.PassageCollections | Where-Object { $_.Id -eq "scripture" } | Select-Object -First 1
      if (-not $scriptureTask) { throw "Default scripture task missing." }
      if (-not $scripture) { throw "Default scripture collection missing." }
      if ($scriptureTask.Task.PassageCollectionId -ne "scripture") { throw "Scripture task references an invalid collection." }
      if (@($scripture.Passages).Count -lt 4) { throw "Expected Bible and Book of Mormon passages were not loaded." }
      if (-not ($scripture.Passages -match "John 14:15") -or -not ($scripture.Passages -match "1 Nephi 3:7")) {
        throw "Expected Bible and Book of Mormon passages were not found."
      }
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

  # Reproduce the original 1.0.2 bug: Parent Settings saved blank references.
  # Verify upgrading does not erase custom whitelisted sites or the PIN.
  Stop-ScheduledTask -TaskName "HomeFocus Agent" -ErrorAction Stop
  Start-Sleep -Seconds 2
  $legacy = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
  $legacy.SchemaVersion = 1
  $legacy.AlwaysAllowedDomains += "school.example.org"
  $legacy.RewardGroups[0].Name = "YouTube"
  $legacy.Tasks[0].RewardGroupId = ""
  $legacy.Tasks[1].RewardGroupId = ""
  $legacy.Tasks[1].PassageCollectionId = ""
  $legacy.PassageCollections | Where-Object Id -eq "scripture" | ForEach-Object { $_.Passages = @() }
  $legacy | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding UTF8
  Start-ScheduledTask -TaskName "HomeFocus Agent" -ErrorAction Stop
  Start-Sleep -Seconds 5
  $restored = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
  if ($restored.SchemaVersion -ne 2) { throw "Migration did not advance config schema." }
  if ($restored.Tasks[1].PassageCollectionId -ne "scripture") { throw "Migration did not restore scripture task reference." }
  if ($restored.Tasks[1].RewardGroupId -ne "youtube") { throw "Migration did not restore reward link." }
  if ($restored.Tasks[0].RewardGroupId -ne "youtube") { throw "Migration did not restore the ice task reward." }
  if (@($restored.PassageCollections | Where-Object Id -eq "scripture" | Select-Object -ExpandProperty Passages).Count -lt 4) {
    throw "Migration did not restore sample scripture passages."
  }
  if (@($restored.AlwaysAllowedDomains) -notcontains "school.example.org") { throw "Migration erased a custom whitelist entry." }
  if ($restored.RewardGroups[0].Name -eq "YouTube") { throw "Legacy reward group name was not migrated." }
  Write-Host "Legacy empty-passage and missing-reward migration passed."
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
