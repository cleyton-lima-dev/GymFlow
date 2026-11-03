; Homologation-only Windows installer for the Flutter Desktop Release folder.
; This uses a separate AppId so it cannot upgrade/replace the future Production app.
; Build the Flutter app from a clean, identified source tree before compiling this file.

#ifndef AppSource
  #error AppSource must point to the clean Flutter Windows Release directory.
#endif
#ifndef BuildMetadata
  #error BuildMetadata must point to the generated build manifest.
#endif
#ifndef InstallerOutput
  #error InstallerOutput must be a new, exclusive output directory.
#endif
#ifndef AppVersion
  #error AppVersion is required.
#endif
#ifndef BuildNumber
  #error BuildNumber is required.
#endif
#ifndef CommitShort
  #error CommitShort is required.
#endif

#define AppName "Avelri Gestão - Homologação"
#define AppExecutable "avelri_gestao.exe"

[Setup]
; Stable homologation identity. Keep this separate from the future Production AppId.
AppId={{D68C6C5D-2D87-4CBA-9918-3AD08C0D1C69}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}+{#BuildNumber}
AppPublisher=Avelri
AppCopyright=Copyright (C) Avelri
DefaultDirName={localappdata}\Programs\Avelri\Gestao-Homologacao
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
Uninstallable=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExecutable}
OutputDir={#InstallerOutput}
OutputBaseFilename=AvelriGestao-Homologacao-{#AppVersion}+{#BuildNumber}-{#CommitShort}
VersionInfoVersion={#AppVersion}.{#BuildNumber}
VersionInfoProductVersion={#AppVersion}.{#BuildNumber}
VersionInfoCompany=Avelri
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} build {#AppVersion}+{#BuildNumber} ({#CommitShort})
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.ilk,*.obj,*.exp,*.lib,*.map"
Source: "{#BuildMetadata}"; DestDir: "{app}"; DestName: "Avelri-Build-Info.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Avelri Gestão - Homologação"; Filename: "{app}\{#AppExecutable}"
Name: "{autodesktop}\Avelri Gestão - Homologação"; Filename: "{app}\{#AppExecutable}"; Tasks: desktopicon

; Intentionally no [Run] or [UninstallDelete]: installing does not start the app,
; and uninstall does not remove user data or credentials stored outside {app}.
