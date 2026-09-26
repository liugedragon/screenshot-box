#ifndef AppVersion
  #define AppVersion "0.1.3"
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

[CustomMessages]
chinesesimp.DesktopShortcut=创建桌面快捷方式
chinesesimp.Shortcuts=快捷方式：
chinesesimp.LaunchProgram=启动截图资料盒
english.DesktopShortcut=Create a desktop shortcut
english.Shortcuts=Shortcuts:
english.LaunchProgram=Open ScreenshotBox

[Icons]
Name: "{group}\ScreenshotBox"; Filename: "{app}\ScreenshotBox.exe"
Name: "{autodesktop}\ScreenshotBox"; Filename: "{app}\ScreenshotBox.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ScreenshotBox.exe"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

; Library data lives separately in LocalAppData\ScreenshotBox or the user-selected directory.
; Do not add an UninstallDelete entry for that directory.
