#ifndef AppVersion
  #define AppVersion "0.1.4"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\ScreenshotBox-" + AppVersion + "-win-x64"
#endif

[Setup]
AppId={{45420B2B-8CAE-4AAD-AD09-C0D675AE541A}
AppName=ScreenshotBox
AppVersion={#AppVersion}
AppPublisher=ScreenshotBox contributors
DefaultDirName={localappdata}\Programs\ScreenshotBox
DefaultGroupName=ScreenshotBox
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
SetupIconFile=..\assets\screenshotbox.ico
OutputDir=..\artifacts
OutputBaseFilename=ScreenshotBox-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ScreenshotBox.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "installer\*"

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked
Name: "autostart"; Description: "{cm:StartAtSignIn}"; GroupDescription: "{cm:Startup}"

[CustomMessages]
chinesesimp.DesktopShortcut=创建桌面快捷方式
chinesesimp.Shortcuts=快捷方式：
chinesesimp.LaunchProgram=在托盘启动截图资料盒
chinesesimp.Startup=自动启动：
chinesesimp.StartAtSignIn=登录 Windows 后在托盘启动
chinesesimp.StartupPathTooLong=自动启动命令超过 Windows 的 260 字符限制。
chinesesimp.StartupPathInstructions=请返回缩短安装路径，或取消“登录 Windows 后在托盘启动”。
english.DesktopShortcut=Create a desktop shortcut
english.Shortcuts=Shortcuts:
english.LaunchProgram=Start ScreenshotBox in the tray
english.Startup=Startup:
english.StartAtSignIn=Start in the tray when I sign in to Windows
english.StartupPathTooLong=The startup command exceeds the Windows limit of 260 characters.
english.StartupPathInstructions=Go back and choose a shorter installation path, or clear "Start in the tray when I sign in to Windows".

[Icons]
Name: "{group}\ScreenshotBox"; Filename: "{app}\ScreenshotBox.exe"
Name: "{autodesktop}\ScreenshotBox"; Filename: "{app}\ScreenshotBox.exe"; Tasks: desktopicon

[Registry]
; Keep the value name stable so Windows retains the user's startup enable/disable state.
; StartupApproved is owned by Windows and must not be rewritten by Setup.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScreenshotBox"; ValueData: "{code:StartupCommand}"; Tasks: autostart; Flags: uninsdeletevalue
; Removing a previously selected task must also remove its Run registration on upgrade.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ScreenshotBox"; Tasks: not autostart; Flags: deletevalue

[Run]
Filename: "{app}\ScreenshotBox.exe"; Parameters: "--background"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

; Library data lives separately in LocalAppData\ScreenshotBox or the user-selected directory.
; Do not add an UninstallDelete entry for that directory.

[Code]
function StartupCommand(Param: String): String;
begin
  Result := '"' + ExpandConstant('{app}\ScreenshotBox.exe') + '" --background';
end;

function StartupPathError(): String;
begin
  Result := ExpandConstant('{cm:StartupPathTooLong}') + #13#10 +
    ExpandConstant('{cm:StartupPathInstructions}');
end;

function StartupCommandTooLong(): Boolean;
begin
  Result := WizardIsTaskSelected('autostart') and (Length(StartupCommand('')) > 260);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if ((CurPageID = wpSelectTasks) or (CurPageID = wpReady)) and StartupCommandTooLong() then
  begin
    Log(StartupPathError());
    if not WizardSilent then
      MsgBox(StartupPathError(), mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  { Covers silent installs and changes made after the additional-tasks page. }
  if StartupCommandTooLong() then
    Result := StartupPathError();
end;
