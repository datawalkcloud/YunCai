; 云财税务助手安装包（Inno Setup 6）
; 构建：scripts/build-tax-assistant.ps1，会传入 AppVersion / SourceDir / OutputDir。
; 按当前用户安装到 %LOCALAPPDATA%，全程不需要管理员权限。

#ifndef AppVersion
  #define AppVersion "1.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\obj\installer-stage"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\..\ui\public\downloads"
#endif

#define AppName "云财税务助手"
#define AppExe "KuaijiTool.exe"
#define RunValueName "KuaijiTaxAssistant"

[Setup]
AppId={{6C2D9E31-8B47-4A5C-9E7F-3D1A5B8C0E42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=云财
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=KuaijiTaxAssistantSetup
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
LanguageDetectionMethod=none
MinVersion=10.0.14393
; 与 App.xaml.cs 的互斥体同名：安装/升级/卸载时提示先关闭正在运行的助手。
AppMutex=KuaijiTaxAssistant
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce
Name: "autostart"; Description: "开机自动启动助手（推荐，网页才能随时检测到助手）"; GroupDescription: "启动选项："; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\卸载{#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; 助手自己启动时也会校正这两个键；这里先写一遍，安装完立刻点网页上的「登录税局」也能唤起。
Root: HKCU; Subkey: "Software\Classes\kuaiji"; ValueType: string; ValueName: ""; ValueData: "URL:{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\kuaiji"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\kuaiji\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#RunValueName}"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "立即启动{#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2ClientsKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  WebView2DownloadUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

function HasWebView2Runtime(): Boolean;
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKCU, WebView2ClientsKey, 'pv', Version) or
    RegQueryStringValue(HKLM, WebView2ClientsKey, 'pv', Version) or
    (IsWin64 and RegQueryStringValue(HKLM64, WebView2ClientsKey, 'pv', Version));
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not HasWebView2Runtime() then
  begin
    if MsgBox('未检测到 Microsoft Edge WebView2 运行时，助手启动后无法打开电子税务局。' + #13#10 + #13#10 +
      '建议先安装 WebView2 运行时：' + #13#10 + WebView2DownloadUrl + #13#10 + #13#10 +
      '是否仍要继续安装？', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { 升级安装时用户可能取消勾选开机自启，要清掉上次留下的启动项。 }
  if (CurStep = ssPostInstall) and (not WizardIsTaskSelected('autostart')) then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
end;
