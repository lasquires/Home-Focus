$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $root "src/HomeFocus.Shared/HomeFocus.Shared.csproj") -c Release
dotnet build (Join-Path $root "src/HomeFocus.Agent/HomeFocus.Agent.csproj") -c Release
dotnet build (Join-Path $root "src/HomeFocus.App/HomeFocus.App.csproj") -c Release
dotnet build (Join-Path $root "src/HomeFocus.Updater/HomeFocus.Updater.csproj") -c Release
Write-Host "All HomeFocus Windows projects compiled successfully."
