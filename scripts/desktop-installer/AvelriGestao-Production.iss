; Production Windows installer for the Flutter Desktop Release folder.
; Build only from the exact reviewed Git commit using Build-ProductionInstaller.ps1.

#ifndef AppSource
  #error AppSource must point to the clean Flutter Windows Release directory.
#endif
#ifndef BuildMetadata
  #error BuildMetadata must point to the generated build manifest.
#endif
#ifndef InstallerOutput
  #error InstallerOutput must point to a new, exclusive output directory.
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

#define AppName "Avelri Gestão"
#define AppExecutable "avelri_gestao.exe"

[Setup]
; Stable Production identity. Deliberately distinct from the homologation AppId.
AppId={{B46B7D1A-6E54-4C0A-A1E2-5F41C6D92837}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}+{#BuildNumber}
AppPublisher=Avelri
AppCopyright=Copyright (C) Avelri
DefaultDirName={localappdata}\Programs\Avelri\Gestao
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExecutable}
OutputDir={#InstallerOutput}
OutputBaseFilename=AvelriGestao-Production-{#AppVersion}+{#BuildNumber}-{#CommitShort}
VersionInfoVersion={#AppVersion}.{#BuildNumber}
VersionInfoProductVersion={#AppVersion}.{#BuildNumber}
VersionInfoCompany=Avelri
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Production build {#AppVersion}+{#BuildNumber} ({#CommitShort})
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.ilk,*.obj,*.exp,*.lib,*.map"
Source: "{#BuildMetadata}"; DestDir: "{app}"; DestName: "Avelri-Build-Info.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Avelri Gestão"; Filename: "{app}\{#AppExecutable}"
Name: "{autodesktop}\Avelri Gestão"; Filename: "{app}\{#AppExecutable}"; Tasks: desktopicon

; No [Run] or [UninstallDelete]: setup never launches the app and uninstall
; does not remove application-support files or credentials outside {app}.
