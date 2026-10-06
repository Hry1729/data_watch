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
; ✅ 新增：安装器嵌入 UAC 清单，双击时自动弹出 UAC 提示（不再静默失败）
PrivilegesRequiredOverridesAllowed=commandline
OutputDir={#SrcRoot}\installer\Output
OutputBaseFilename=DataWatch-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
; 安装包(Setup.exe)自身图标用 Tauri 的自定义图标，替换 Inno 默认图标
SetupIconFile={#SrcRoot}\app\src-tauri\icons\icon.ico
; 「添加/删除程序」卸载项图标用装好的 app.exe(已内嵌同一图标)
UninstallDisplayIcon={app}\app\app.exe

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
; ── 工具：NSSM 服务包装器 + 性能基线采集 ──
Source: "{#SrcRoot}\installer\win64\nssm.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "{#SrcRoot}\scripts\capture_performance_baseline.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion
; ── 配置模板(脱敏；config.toml.example 直接取自 agent，settings 取自 installer\templates 的脱敏版) ──
Source: "{#SrcRoot}\agent\config.toml.example"; DestDir: "{app}\templates"; DestName: "config.toml.example"; Flags: ignoreversion
Source: "{#SrcRoot}\installer\templates\collector-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
Source: "{#SrcRoot}\installer\templates\writer-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
; ── WebView2 evergreen 引导安装器(装到 {tmp}，装完即删；离线机可替换为 ~127MB standalone 同名文件) ──
Source: "{#SrcRoot}\installer\webview2\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[InstallDelete]
; 覆盖升级时先清掉旧 onedir 产物，避免已从新版本移除的 _internal 文件残留。
; ProgramData 配置与运行数据不在 {app} 下，不受影响。
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\agent"
Type: filesandordirs; Name: "{app}\collector"
Type: filesandordirs; Name: "{app}\writer"
Type: filesandordirs; Name: "{app}\sidecars"

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
var rc: Integer; NssmPath: String;
begin
  NssmPath := ExpandConstant('{app}\tools\nssm.exe');
  if not FileExists(NssmPath) then
  begin
    Result := -1;
    Exit;
  end;
  if not Exec(NssmPath, Params, '', SW_HIDE, ewWaitUntilTerminated, rc) then
    rc := -1;
  Result := rc;
end;

// sc query：存在返回 0，不存在通常为 1060。不依赖 NSSM 退出码（无提权时 NSSM 常误报 0）。
function ServiceExists(const Name: String): Boolean;
var rc: Integer;
begin
  if not Exec(ExpandConstant('{sys}\sc.exe'), 'query ' + Name, '', SW_HIDE, ewWaitUntilTerminated, rc) then
    rc := -1;
  Result := (rc = 0);
end;

function WaitServiceGone(const Name: String; TimeoutMs: Integer): Boolean;
var elapsed: Integer;
begin
  elapsed := 0;
  while elapsed <= TimeoutMs do
  begin
    if not ServiceExists(Name) then
    begin
      Result := True;
      Exit;
    end;
    Sleep(500);
    elapsed := elapsed + 500;
  end;
  Result := not ServiceExists(Name);
end;

// DeleteFlag=1：sc delete 已受理，但 SCM 仍占着服务名（开着「服务」窗口或需重启）
function IsServiceMarkedForDelete(const Name: String): Boolean;
var flag: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(
       HKLM, 'SYSTEM\CurrentControlSet\Services\' + Name, 'DeleteFlag', flag) then
    Result := (flag <> 0);
end;

function RebootToFinishServiceMsg(const Name: String): String;
begin
  Result :=
    '升级需要重启电脑一次。' + #13#10#13#10 +
    '原因：旧服务 ' + Name + ' 已被系统标记删除，但 Windows 尚未释放服务名' + #13#10 +
    '（常见于上次安装中断，或打开着「服务」管理窗口）。' + #13#10#13#10 +
    '请按下列步骤操作：' + #13#10 +
    '1. 关闭本安装程序' + #13#10 +
    '2. 重启电脑' + #13#10 +
    '3. 重启后再次运行本安装程序（无需手动执行 sc 命令）' + #13#10#13#10 +
    '重启后旧服务会自动消失，第二次安装即可正常完成。';
end;

// 等到 STOPPED 或不存在，避免对仍在停止中的服务 delete → 卡在「标记删除」
function StopServiceFully(const Name: String; TimeoutMs: Integer): Boolean;
var rc: Integer; elapsed: Integer; qrc: Integer;
begin
  elapsed := 0;
  while elapsed <= TimeoutMs do
  begin
    if not ServiceExists(Name) then
    begin
      Result := True;
      Exit;
    end;

    // ✅ 改进：先尝试优雅停止，再强制停止，最后强杀进程
    Nssm('stop ' + Name);
    Sleep(1000);  // 给服务 1 秒优雅停止时间

    Exec(ExpandConstant('{sys}\sc.exe'), 'stop ' + Name, '', SW_HIDE, ewWaitUntilTerminated, rc);
    Sleep(500);

    // 如果服务还活着，强杀进程
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM data-watch-agent.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);

    // STATE=1 STOPPED 时 sc query 仍成功；用 find 探测 STOPPED 字符串
    if Exec(ExpandConstant('{cmd}'),
         '/C sc query ' + Name + ' | find "STOPPED" >NUL',
         '', SW_HIDE, ewWaitUntilTerminated, qrc) then
    begin
      if qrc = 0 then
      begin
        Result := True;
        Exit;
      end;
    end;

    Sleep(500);
    elapsed := elapsed + 500;
  end;
  Result := (not ServiceExists(Name)) or
    (Exec(ExpandConstant('{cmd}'),
          '/C sc query ' + Name + ' | find "STOPPED" >NUL',
          '', SW_HIDE, ewWaitUntilTerminated, qrc) and (qrc = 0));
end;

// 覆盖升级：充分停服 → remove/delete → 等待消失。
// 若已是「标记删除」，尝试强制清理注册表（管理员权限下）。
function EnsureServiceRemoved(const Name: String): String;
var rc: Integer; RegKey: String;
begin
  Result := '';
  if not ServiceExists(Name) then
    Exit;

  // ✅ 改进：一进来就是标记删除，尝试强制清理
  if IsServiceMarkedForDelete(Name) then
  begin
    StopServiceFully(Name, 5000);

    // 尝试手动删除注册表（需管理员权限）
    RegKey := 'SYSTEM\CurrentControlSet\Services\' + Name;
    if IsAdminInstallMode then
    begin
      // 先用 reg delete 尝试清理
      Exec(ExpandConstant('{sys}\reg.exe'),
           'delete "HKLM\' + RegKey + '" /f',
           '', SW_HIDE, ewWaitUntilTerminated, rc);
      Sleep(1000);

      // 再检查是否清理成功
      if not ServiceExists(Name) then
        Exit;
    end;

    // 如果注册表清理后服务还在，短等一会
    if WaitServiceGone(Name, 8000) then
      Exit;

    // 实在不行，提示重启
    Result := RebootToFinishServiceMsg(Name);
    Exit;
  end;

  if not StopServiceFully(Name, 30000) then
  begin
    if not IsAdminInstallMode then
      Result :=
        '无法停止旧服务 ' + Name + '：请右键安装包选择「以管理员身份运行」后重试。'
    else
      Result :=
        '无法停止旧服务 ' + Name + '。请关闭 Data Watch 后重试；若仍失败，重启电脑后再安装。';
    Exit;
  end;

  Nssm('remove ' + Name + ' confirm');
  if ServiceExists(Name) then
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete ' + Name, '', SW_HIDE, ewWaitUntilTerminated, rc);

  if WaitServiceGone(Name, 15000) then
    Exit;

  // delete 后进入标记删除：再短等一会（用户可能刚关掉服务窗口）
  if IsServiceMarkedForDelete(Name) then
  begin
    // ✅ 改进：尝试强制清理注册表
    if IsAdminInstallMode then
    begin
      RegKey := 'SYSTEM\CurrentControlSet\Services\' + Name;
      Exec(ExpandConstant('{sys}\reg.exe'),
           'delete "HKLM\' + RegKey + '" /f',
           '', SW_HIDE, ewWaitUntilTerminated, rc);
      Sleep(2000);
    end;

    if WaitServiceGone(Name, 10000) then
      Exit;
    Result := RebootToFinishServiceMsg(Name);
    Exit;
  end;

  if not IsAdminInstallMode then
    Result :=
      '无法移除旧服务 ' + Name + '：请右键安装包选择「以管理员身份运行」后重试。'
  else
    Result := RebootToFinishServiceMsg(Name);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  // ✅ 改进：不再弹窗拒绝，而是依赖 UAC 清单自动提权
  // PrivilegesRequiredOverridesAllowed=commandline 会让 Windows 自动弹出 UAC 对话框
  // 用户点"是"后以管理员身份重新启动安装器，无需手动右键
  if not IsAdminInstallMode then
  begin
    // 这里不再 MsgBox，让 Windows UAC 自然接管
    // 如果用户在 UAC 点了"否"，安装器会自动退出
    Result := False;
    Exit;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var rc: Integer; OldNssm: String;
begin
  Result := '';
  NeedsRestart := False;
  OldNssm := ExpandConstant('{app}\tools\nssm.exe');

  // 覆盖文件前先停旧 Agent，防止 agent/collector/writer/proxy 占用安装目录。
  if FileExists(OldNssm) then
    Exec(OldNssm, 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, rc);

  // app.exe 由 Inno Restart Manager 按目标文件路径关闭，避免误杀其他同名程序。
  // 这里只兜底结束 DataWatch 专用受管进程；未运行时 taskkill 非零返回属于正常情况。
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM data-watch-agent.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM data-watch-collector.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM data-watch-writer.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OpcDaProxyService.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);

  // 覆盖升级：换文件前必须清掉旧服务；若已标记删除则中止并提示重启后再装
  Result := EnsureServiceRemoved('{#MyServiceName}');
  if Result <> '' then
  begin
    // 让向导提示重启（用户重启后再次运行安装包即可）
    NeedsRestart := True;
  end;
end;

procedure NssmRequired(StepName, Params: String);
var rc: Integer;
begin
  rc := Nssm(Params);
  if rc <> 0 then
  begin
    if IsServiceMarkedForDelete('{#MyServiceName}') then
      RaiseException(RebootToFinishServiceMsg('{#MyServiceName}'))
    else if (rc = 5) and (not IsAdminInstallMode) then
      RaiseException(
        'NSSM ' + StepName + ' 失败：拒绝访问。' + #13#10 +
        '请右键安装包选择「以管理员身份运行」后重试。')
    else if rc = 5 then
      RaiseException(RebootToFinishServiceMsg('{#MyServiceName}'))
    else
      RaiseException('NSSM ' + StepName + ' 失败 (rc=' + IntToStr(rc) + ')');
  end;
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

function GenerateRandomToken: String;
var
  i: Integer;
  chars: String;
begin
  // 生成 64 字符的随机 token (Base64-URL-safe 字符集)
  chars := 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
  Result := '';
  for i := 1 to 64 do
    Result := Result + chars[Random(Length(chars)) + 1];
end;

function TrimString(const S: String): String;
var
  i, L: Integer;
begin
  Result := S;
  // 去除前导空白
  i := 1;
  L := Length(Result);
  while (i <= L) and ((Result[i] = ' ') or (Result[i] = #13) or (Result[i] = #10) or (Result[i] = #9)) do
    Inc(i);
  if i > 1 then
    Delete(Result, 1, i - 1);
  // 去除尾部空白
  L := Length(Result);
  while (L > 0) and ((Result[L] = ' ') or (Result[L] = #13) or (Result[L] = #10) or (Result[L] = #9)) do
    Dec(L);
  SetLength(Result, L);
end;

procedure RegisterService;
var app, data, exe, err, token, tokenFile: String; rc: Integer; lines: TStringList;
begin
  if not IsAdminInstallMode then
    RaiseException(
      '注册服务需要管理员权限。请右键安装包选择「以管理员身份运行」。');

  app := ExpandConstant('{app}');
  data := ExpandConstant('{commonappdata}\DataWatch');
  exe := app + '\agent\data-watch-agent.exe';

  // 生成或读取已有的 SERVICE_TOKEN
  tokenFile := data + '\agent_service_token.txt';
  token := '';
  if FileExists(tokenFile) then
  begin
    // 升级安装：保留原有 token
    lines := TStringList.Create;
    try
      lines.LoadFromFile(tokenFile);
      if lines.Count > 0 then
        token := TrimString(lines[0]);
    finally
      lines.Free;
    end;
  end;
  if token = '' then
  begin
    // 全新安装：生成新 token
    token := GenerateRandomToken;
    lines := TStringList.Create;
    try
      lines.Add(token);
      lines.SaveToFile(tokenFile);
    finally
      lines.Free;
    end;
  end;

  // 幂等：再清一次（防止 PrepareToInstall 之后服务被拉起），再全新注册
  err := EnsureServiceRemoved('{#MyServiceName}');
  if err <> '' then
    RaiseException(err);

  rc := Nssm('install {#MyServiceName} "' + exe + '"');
  // 偶发「服务仍在」：再清一次并重试 install
  if (rc <> 0) and ServiceExists('{#MyServiceName}') then
  begin
    err := EnsureServiceRemoved('{#MyServiceName}');
    if err <> '' then
      RaiseException(err);
    rc := Nssm('install {#MyServiceName} "' + exe + '"');
  end;
  if rc <> 0 then
  begin
    if IsServiceMarkedForDelete('{#MyServiceName}') or ServiceExists('{#MyServiceName}') then
      RaiseException(RebootToFinishServiceMsg('{#MyServiceName}'));
    RaiseException('NSSM install 失败 (rc=' + IntToStr(rc) + ')');
  end;

  NssmRequired('set AppDirectory', 'set {#MyServiceName} AppDirectory "' + app + '\agent"');
  // 三个根环境变量须在进程启动前注入(CONFIG_PATH 在 import 期计算)
  NssmRequired('set AppEnvironmentExtra', 'set {#MyServiceName} AppEnvironmentExtra "DATAWATCH_INSTALL_ROOT=' + app + '" "DATAWATCH_DATA_ROOT=' + data + '" "DATAWATCH_AGENT_SERVICE_TOKEN=' + token + '"');
  NssmRequired('set Start', 'set {#MyServiceName} Start SERVICE_AUTO_START');
  NssmRequired('set AppStopMethodConsole', 'set {#MyServiceName} AppStopMethodConsole 60000');
  NssmRequired('set AppStopMethodWindow', 'set {#MyServiceName} AppStopMethodWindow 1500');
  NssmRequired('set AppStopMethodThreads', 'set {#MyServiceName} AppStopMethodThreads 1500');
  NssmRequired('set AppStdout', 'set {#MyServiceName} AppStdout "' + data + '\runtime\logs\agent-service.out.log"');
  NssmRequired('set AppStderr', 'set {#MyServiceName} AppStderr "' + data + '\runtime\logs\agent-service.err.log"');
  NssmRequired('start', 'start {#MyServiceName}');
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
var rc: Integer; data: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // 先停止并删除服务，释放 ProgramData 下日志/pid/state 等文件占用
    EnsureServiceRemoved('{#MyServiceName}');
    // 兜底清理受管子进程。正常情况下停 Agent 服务即可；这里 best-effort，忽略返回码。
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM data-watch-collector.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM data-watch-writer.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OpcDaProxyService.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    data := ExpandConstant('{commonappdata}\DataWatch');
    if DirExists(data) then
    begin
      if UninstallSilent() or
         (MsgBox('是否同时删除配置与运行数据？' + #13#10#13#10 +
                 data + #13#10#13#10 +
                 '选「是」彻底清理（重装将重新进入初始化向导）；' + #13#10 +
                 '选「否」保留配置（重装后沿用旧配置）。',
                 mbConfirmation, MB_YESNO) = IDYES) then
        DelTree(data, True, True, True);
    end;
  end;
end;
