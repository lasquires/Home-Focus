#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif
#ifndef SourceRoot
  #define SourceRoot ".."
#endif
#ifndef OutputDir
  #define OutputDir ".."
#endif

[Setup]
AppId={{5D523A28-1169-4C0E-97AC-B397E4A04292}
AppName=HomeFocus
AppVersion={#AppVersion}
AppPublisher=HomeFocus
DefaultDirName={autopf}\HomeFocus
DefaultGroupName=HomeFocus
DisableDirPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=HomeFocus-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
InfoBeforeFile={#SourceRoot}\installer\INSTALL-NOTICE.txt
UninstallDisplayName=HomeFocus
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#SourceRoot}\dist\HomeFocus.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\dist\HomeFocus.Agent.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\dist\HomeFocus.Updater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\dist\version.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\Install.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\Uninstall.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\Emergency-Unlock.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\HomeFocus"; Filename: "{app}\HomeFocus.exe"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Install.ps1"" -DistPath ""{app}"" -NoStart"; StatusMsg: "Registering HomeFocus agent and startup..."; Flags: runhidden waituntilterminated
Filename: "{app}\HomeFocus.exe"; Description: "Launch HomeFocus and set the parent PIN"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Uninstall.ps1"" -KeepFiles"; Flags: runhidden waituntilterminated
