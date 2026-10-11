param(
  [string]$DistPath = "$PSScriptRoot\dist",
  [switch]$NoStart
)
$ErrorActionPreference = "Stop"
$logPath = Join-Path $env:ProgramData "HomeFocus-install.log"
function Write-InstallLog([string]$message) {
  Add-Content -LiteralPath $logPath -Value ("{0:u} {1}" -f (Get-Date), $message)
}

try {
  Write-InstallLog "Starting HomeFocus installation. Source: $DistPath"
  $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "HomeFocus installer requires administrator privileges." }

  $install = Join-Path $env:ProgramFiles "HomeFocus"
  $data = Join-Path $env:ProgramData "HomeFocus"
  $required = @("HomeFocus.exe","HomeFocus.Agent.exe","HomeFocus.Updater.exe")
  foreach($f in $required) {
    if(-not (Test-Path (Join-Path $DistPath $f))) { throw "Missing installation file $f." }
  }

  New-Item -ItemType Directory -Force -Path $install,$data | Out-Null
  # Stop an older agent before updating executables.
  if (Get-ScheduledTask -TaskName "HomeFocus Agent" -ErrorAction SilentlyContinue) {
    try { & schtasks.exe /End /TN "HomeFocus Agent" 2>$null | Out-Null } catch { }
  }
  Get-Process HomeFocus,HomeFocus.Agent -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

  if (-not [string]::Equals(
    [IO.Path]::GetFullPath($DistPath).TrimEnd('\'),
    [IO.Path]::GetFullPath($install).TrimEnd('\'),
    [StringComparison]::OrdinalIgnoreCase
  )) {
    Copy-Item (Join-Path $DistPath '*') $install -Force
  }

  & icacls.exe $data /inheritance:r /grant:r "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Could not secure the HomeFocus data directory (icacls exit $LASTEXITCODE)." }

  $agent = Join-Path $install "HomeFocus.Agent.exe"
  if (-not (Test-Path -LiteralPath $agent)) { throw "Agent executable missing: $agent" }

  # Use Windows ScheduledTasks' structured Execute field. schtasks.exe /TR
  # can mishandle quoting a path inside Program Files (0x80070002).
  $taskAction = New-ScheduledTaskAction -Execute $agent -WorkingDirectory $install
  $taskTrigger = New-ScheduledTaskTrigger -AtStartup
  $taskPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
  $taskSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
  Register-ScheduledTask -TaskName "HomeFocus Agent" -Action $taskAction -Trigger $taskTrigger -Principal $taskPrincipal -Settings $taskSettings -Force | Out-Null
  $savedTask = Get-ScheduledTask -TaskName "HomeFocus Agent"
  Write-InstallLog ("Registered startup task. Action Execute: " + $savedTask.Actions[0].Execute)
  Start-ScheduledTask -TaskName "HomeFocus Agent" -ErrorAction Stop
  Write-InstallLog "Requested agent startup."

  $runKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
  New-ItemProperty -Path $runKey -Name "HomeFocus" -Value ('"{0}"' -f (Join-Path $install "HomeFocus.exe")) -PropertyType String -Force | Out-Null

  # Check that the agent is actually listening, not just that schtasks accepted /Run.
  $healthy = $false
  for ($i=0; $i -lt 20; $i++) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
      $result = $client.BeginConnect("127.0.0.1",48191,$null,$null)
      if ($result.AsyncWaitHandle.WaitOne(500)) {
        $client.EndConnect($result)
        $healthy = $true
        break
      }
    } catch {}
    finally { $client.Close() }
    Start-Sleep -Milliseconds 500
  }
  if (-not $healthy) {
    $agentLog = Join-Path $data "HomeFocus.log"
    if (Test-Path $agentLog) {
      Write-InstallLog ("Recent agent log: " + ((Get-Content $agentLog -Tail 12) -join " | "))
    }
    $taskInfo = Get-ScheduledTaskInfo -TaskName "HomeFocus Agent" -ErrorAction SilentlyContinue
    if ($taskInfo) { Write-InstallLog "Task LastTaskResult: $($taskInfo.LastTaskResult)" }
    throw "HomeFocus Agent did not respond on localhost:48191 after installation. See $logPath."
  }
  Write-InstallLog "HomeFocus Agent is responding on port 48191."
  if (-not $NoStart) { Start-Process (Join-Path $install "HomeFocus.exe") }
  Write-InstallLog "Installation successful."
}
catch {
  Write-InstallLog ("INSTALLATION FAILED: " + $_.ToString())
  Write-Error ("HomeFocus installation failed. See " + $logPath + ". " + $_.Exception.Message)
  exit 1
}
