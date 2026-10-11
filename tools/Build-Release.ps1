param(
  [string]$Version = "1.0.0",
  [string]$Runtime = "win-x64",
  [string]$Owner = "lasquires",
  [string]$Repo = "Home-Focus"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$stage = Join-Path $root ".stage"
Remove-Item $dist,$stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $dist,$stage | Out-Null

$projects = @(
  @{ Path="src/HomeFocus.App/HomeFocus.App.csproj"; Exe="HomeFocus.exe" },
  @{ Path="src/HomeFocus.Agent/HomeFocus.Agent.csproj"; Exe="HomeFocus.Agent.exe" },
  @{ Path="src/HomeFocus.Updater/HomeFocus.Updater.csproj"; Exe="HomeFocus.Updater.exe" }
)
foreach ($p in $projects) {
  $out = Join-Path $stage ([IO.Path]::GetFileNameWithoutExtension($p.Path))
  dotnet publish (Join-Path $root $p.Path) -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version -o $out
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($p.Path) with code $LASTEXITCODE" }
  Copy-Item (Join-Path $out $p.Exe) (Join-Path $dist $p.Exe)
}
Set-Content -Path (Join-Path $dist "version.txt") -Value $Version -Encoding ASCII

$zip = Join-Path $root "HomeFocus-Windows-$Version.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $zip -CompressionLevel Optimal
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
  version = $Version
  packageUrl = "https://github.com/$Owner/$Repo/releases/download/v$Version/HomeFocus-Windows-$Version.zip"
  sha256 = $sha
  notes = "Built from HomeFocus Windows source."
}
$manifest | ConvertTo-Json | Set-Content (Join-Path $root "update-manifest.json") -Encoding UTF8
Write-Host "Built $zip"
Write-Host "SHA256 $sha"
Write-Host "Commit update-manifest.json to main and attach the ZIP to GitHub release v$Version."
