; DataWatch 边缘机一体安装包 (Inno Setup 6)
; 装：app(Tauri) + agent + collector + writer + opcda proxy sidecar；注册 DataWatchAgent 服务(NSSM)。
; 目标机 Windows 10：缺 WebView2 运行时则静默安装。
; 源路径以本 .iss 所在目录(installer\)的上级为仓库根，便于移植。
#define SrcRoot SourcePath + ".."
#define MyAppName "Data Watch"
#define MyAppVersion "0.1.0"
#define MyServiceName "DataWatchAgent"

[Setup]
AppId={{8F3C2A91-4D2E-4B7A-9C1F-DA7AE0C3A1B2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=hry
DefaultDirName={autopf}\DataWatch
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
OutputDir={#SrcRoot}\installer\Output
OutputBaseFilename=DataWatch-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}

[Languages]
Name: "default"; MessagesFile: "compiler:Default.isl"

[Files]
; ── app(Tauri 产物：主程序 + 资源) ──
Source: "{#SrcRoot}\app\src-tauri\target\release\app.exe"; DestDir: "{app}\app"; Flags: ignoreversion
Source: "{#SrcRoot}\app\src-tauri\target\release\resources\*"; DestDir: "{app}\app\resources"; Flags: ignoreversion recursesubdirs createallsubdirs
; ── agent / collector / writer (PyInstaller onedir：exe + _internal) ──
Source: "{#SrcRoot}\agent\dist\data-watch-agent\*"; DestDir: "{app}\agent"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\collector\dist\data-watch-collector\*"; DestDir: "{app}\collector"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\writer\dist\data-watch-writer\*"; DestDir: "{app}\writer"; Flags: ignoreversion recursesubdirs createallsubdirs
; ── OPC DA proxy sidecar (仅 bin/，需目标机 .NET Framework 4.8) ──
Source: "{#SrcRoot}\collector\sidecars\opcda_proxy_service\bin\*"; DestDir: "{app}\sidecars\opcda_proxy_service\bin"; Flags: ignoreversion recursesubdirs createallsubdirs
; ── 工具：NSSM 服务包装器 ──
Source: "{#SrcRoot}\installer\win64\nssm.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
; ── 配置模板(脱敏；config.toml.example 直接取自 agent，settings 取自 installer\templates 的脱敏版) ──
Source: "{#SrcRoot}\agent\config.toml.example"; DestDir: "{app}\templates"; DestName: "config.toml.example"; Flags: ignoreversion
Source: "{#SrcRoot}\installer\templates\collector-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
Source: "{#SrcRoot}\installer\templates\writer-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
; ── WebView2 evergreen 引导安装器(装到 {tmp}，装完即删；离线机可替换为 ~127MB standalone 同名文件) ──
Source: "{#SrcRoot}\installer\webview2\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Dirs]
; 可写数据根(DATAWATCH_DATA_ROOT)；config.toml / settings.toml / runtime 全落这里
Name: "{commonappdata}\DataWatch"
Name: "{commonappdata}\DataWatch\collector\config"
Name: "{commonappdata}\DataWatch\writer\config"
Name: "{commonappdata}\DataWatch\runtime"
Name: "{commonappdata}\DataWatch\runtime\logs"

[Icons]
Name: "{commondesktop}\Data Watch Console"; Filename: "{app}\app\app.exe"; WorkingDir: "{app}\app"
Name: "{group}\Data Watch Console"; Filename: "{app}\app\app.exe"; WorkingDir: "{app}\app"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Win10 缺 WebView2 才装(Win11 自带)；/silent /install 已存在时为 no-op
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft WebView2 Runtime..."; Flags: waituntilterminated; Check: NeedsWebView2

[Code]
const
  WV2_GUID = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function WV2Present(RootKey: Integer; SubKey: String): Boolean;
var v: String;
begin
  Result := RegQueryStringValue(RootKey, SubKey, 'pv', v) and (v <> '') and (v <> '0.0.0.0');
end;

function IsWebView2Installed: Boolean;
begin
  Result :=
    WV2Present(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WV2_GUID) or
    WV2Present(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WV2_GUID) or
    WV2Present(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WV2_GUID);
end;

function NeedsWebView2: Boolean;
begin
  Result := not IsWebView2Installed;
end;

function Nssm(Params: String): Integer;
var rc: Integer;
begin
  if not Exec(ExpandConstant('{app}\tools\nssm.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, rc) then
    rc := -1;
  Result := rc;
end;

procedure CopyIfAbsent(Src, Dst: String);
begin
  if not FileExists(Dst) then
    CopyFile(Src, Dst, False);
end;

procedure ProvisionConfigs;
var app, data: String;
begin
  app := ExpandConstant('{app}');
  data := ExpandConstant('{commonappdata}\DataWatch');
  CopyIfAbsent(app + '\templates\config.toml.example', data + '\config.toml');
  CopyIfAbsent(app + '\templates\collector-settings.toml.example', data + '\collector\config\settings.toml');
  CopyIfAbsent(app + '\templates\writer-settings.toml.example', data + '\writer\config\settings.toml');
end;

procedure RegisterService;
var app, data, exe: String;
begin
  app := ExpandConstant('{app}');
  data := ExpandConstant('{commonappdata}\DataWatch');
  exe := app + '\agent\data-watch-agent.exe';
  // 幂等：先停删已存在的同名服务，再全新注册
  Nssm('stop ' + '{#MyServiceName}');
  Nssm('remove ' + '{#MyServiceName} confirm');
  Nssm('install {#MyServiceName} "' + exe + '"');
  Nssm('set {#MyServiceName} AppDirectory "' + app + '\agent"');
  // 两个根环境变量须在进程启动前注入(CONFIG_PATH 在 import 期计算)
  Nssm('set {#MyServiceName} AppEnvironmentExtra "DATAWATCH_INSTALL_ROOT=' + app + '" "DATAWATCH_DATA_ROOT=' + data + '"');
  Nssm('set {#MyServiceName} Start SERVICE_AUTO_START');
  Nssm('set {#MyServiceName} AppStdout "' + data + '\runtime\logs\agent-service.out.log"');
  Nssm('set {#MyServiceName} AppStderr "' + data + '\runtime\logs\agent-service.err.log"');
  Nssm('start {#MyServiceName}');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ProvisionConfigs;
    RegisterService;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var rc: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // 停止并删除服务；ProgramData 下的数据/配置保留，不随卸载删除
    Exec(ExpandConstant('{app}\tools\nssm.exe'), 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, rc);
    Exec(ExpandConstant('{app}\tools\nssm.exe'), 'remove {#MyServiceName} confirm', '', SW_HIDE, ewWaitUntilTerminated, rc);
  end;
end;
