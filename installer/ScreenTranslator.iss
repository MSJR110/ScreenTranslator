; Inno Setup script — builds installer\Output\ScreenTranslator-Setup-<version>.exe
; Prereq: run publish.ps1 first (dist\ScreenTranslator.exe must exist). Compile with ISCC.exe or the Inno Setup IDE.

#define AppName "ScreenTranslator"
#define AppExe "ScreenTranslator.exe"
#define AppPublisher "ScreenTranslator"
#define DistDir "..\dist"
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString(DistDir + "\" + AppExe)
#endif

[Setup]
AppId={{7E1C1D4B-3E43-4B3C-9C7B-5D2C0F4B6A11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install by default (no UAC), can be elevated by the user.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=..\src\ScreenTranslator\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Start {#AppName} when Windows starts"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#DistDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#AppName}"

[Code]
// The framework-dependent build needs the .NET 10 Desktop Runtime; offer the download page if it's missing.
function IsDotNetDesktopInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c dotnet --list-runtimes | findstr /C:"Microsoft.WindowsDesktop.App 10." >nul', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNetDesktopInstalled() then
  begin
    if MsgBox('ScreenTranslator needs the .NET 10 Desktop Runtime (x64), which is not installed.' + #13#10 + #13#10 +
              'Open the download page now? Setup will continue afterwards.', mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
