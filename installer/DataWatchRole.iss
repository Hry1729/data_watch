; Shared Inno Setup body. Build only through one of the three role wrapper .iss files.
#define SrcRoot SourcePath + ".."

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=hry
DefaultDirName={autopf}\{#MyInstallDir}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
OutputDir={#SrcRoot}\installer\Output
OutputBaseFilename={#MyOutputName}-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupIconFile={#SrcRoot}\app\src-tauri\icons\icon.ico
UninstallDisplayIcon={app}\app\app.exe
; ssPostInstall 阶段（权限加固 / 服务注册）向导没有进度反馈，现场出现过
; 「Finish 点不动、疑似卡死」的反馈且无任何日志可查。SetupLogging 让每次安装
; 都在 %TEMP% 留下带时间戳的 Setup Log，用于定位这类静默停顿。
SetupLogging=yes

[Languages]
Name: "default"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SrcRoot}\app\src-tauri\target\release\app.exe"; DestDir: "{app}\app"; Flags: ignoreversion
Source: "{#SrcRoot}\app\src-tauri\target\release\resources\*"; DestDir: "{app}\app\resources"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#SrcRoot}\agent\dist\data-watch-agent\data-watch-agent.exe"; DestDir: "{app}\agent"; DestName: "{#MyAgentExe}"; Flags: ignoreversion
Source: "{#SrcRoot}\agent\dist\data-watch-agent\_internal\*"; DestDir: "{app}\agent\_internal"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\installer\win64\nssm.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "{#SrcRoot}\installer\webview2\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

#if BuildRole == "centralized"
Source: "{#SrcRoot}\collector\dist\data-watch-collector\*"; DestDir: "{app}\collector"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\writer\dist\data-watch-writer\*"; DestDir: "{app}\writer"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\collector\sidecars\opcda_proxy_service\bin\*"; DestDir: "{app}\sidecars\opcda_proxy_service\bin"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\installer\templates\collector-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
Source: "{#SrcRoot}\installer\templates\writer-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
#endif

#if BuildRole == "edge"
Source: "{#SrcRoot}\edge\dist\data-watch-edge-storage\*"; DestDir: "{app}\edge-storage"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\edge\dist\data-watch-edge-collector\*"; DestDir: "{app}\edge-collector"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\edge\dist\data-watch-uploader\*"; DestDir: "{app}\uploader"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\collector\sidecars\opcda_proxy_service\bin\*"; DestDir: "{app}\sidecars\opcda_proxy_service\bin"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\installer\templates\collector-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
#endif

#if BuildRole == "distributed-center"
Source: "{#SrcRoot}\ingest\dist\data-watch-ingest\*"; DestDir: "{app}\ingest"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\writer\dist\data-watch-writer\*"; DestDir: "{app}\writer"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcRoot}\installer\templates\writer-settings.toml.example"; DestDir: "{app}\templates"; Flags: ignoreversion
#endif

[InstallDelete]
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\agent"
#if BuildRole == "centralized"
Type: filesandordirs; Name: "{app}\collector"
Type: filesandordirs; Name: "{app}\writer"
#endif
#if BuildRole == "edge"
Type: filesandordirs; Name: "{app}\edge-storage"
Type: filesandordirs; Name: "{app}\edge-collector"
Type: filesandordirs; Name: "{app}\uploader"
#endif
#if BuildRole == "distributed-center"
Type: filesandordirs; Name: "{app}\ingest"
Type: filesandordirs; Name: "{app}\writer"
#endif

[Dirs]
Name: "{commonappdata}\{#MyDataDir}"
Name: "{commonappdata}\{#MyDataDir}\runtime"
Name: "{commonappdata}\{#MyDataDir}\runtime\logs"
#if BuildRole == "centralized"
Name: "{commonappdata}\{#MyDataDir}\collector\config"
Name: "{commonappdata}\{#MyDataDir}\writer\config"
#endif
#if BuildRole == "edge"
Name: "{commonappdata}\{#MyDataDir}\collector\config"
Name: "{commonappdata}\{#MyDataDir}\edge"
Name: "{commonappdata}\{#MyDataDir}\storage"
Name: "{commonappdata}\{#MyDataDir}\uploader"
#endif
#if BuildRole == "distributed-center"
Name: "{commonappdata}\{#MyDataDir}\ingest"
Name: "{commonappdata}\{#MyDataDir}\writer\config"
#endif

[Icons]
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\app\app.exe"; WorkingDir: "{app}\app"
Name: "{group}\{#MyAppName}"; Filename: "{app}\app\app.exe"; WorkingDir: "{app}\app"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft WebView2 Runtime..."; Flags: waituntilterminated; Check: NeedsWebView2

[Code]
const
  WV2_GUID = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function WV2Present(RootKey: Integer; SubKey: String): Boolean;
var
  Value: String;
  DotPos: Integer;
begin
  Result := False;
  if not RegQueryStringValue(RootKey, SubKey, 'pv', Value) then Exit;
  DotPos := Pos('.', Value);
  if DotPos <= 1 then Exit;
  { Tailwind 4 requires Chromium 111+; an installed 109 runtime is not sufficient. }
  Result := StrToIntDef(Copy(Value, 1, DotPos - 1), 0) >= 111;
end;

function NeedsWebView2: Boolean;
begin
  Result := not (
    WV2Present(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WV2_GUID) or
    WV2Present(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WV2_GUID) or
    WV2Present(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WV2_GUID));
end;

function Nssm(Params: String): Integer;
var
  ExitCode: Integer;
begin
  if not Exec(ExpandConstant('{app}\tools\nssm.exe'), Params, '', SW_HIDE,
      ewWaitUntilTerminated, ExitCode) then
    ExitCode := -1;
  Result := ExitCode;
end;

function ServiceExists: Boolean;
var
  ExitCode: Integer;
begin
  if not Exec(ExpandConstant('{sys}\sc.exe'), 'query {#MyServiceName}', '', SW_HIDE,
      ewWaitUntilTerminated, ExitCode) then
    ExitCode := -1;
  Result := ExitCode = 0;
end;

procedure StopRoleService;
var
  ExitCode: Integer;
begin
  if not ServiceExists then
    Exit;
  Nssm('stop {#MyServiceName}');
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '', SW_HIDE,
    ewWaitUntilTerminated, ExitCode);
end;

procedure RemoveRoleService;
var
  ExitCode: Integer;
begin
  if not ServiceExists then
    Exit;
  StopRoleService;
  Nssm('remove {#MyServiceName} confirm');
  if ServiceExists then
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyServiceName}', '', SW_HIDE,
      ewWaitUntilTerminated, ExitCode);
end;

procedure StopOrphanedRoleProcesses;
var
  ExitCode: Integer;
begin
#if BuildRole == "edge"
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM data-watch-edge-storage.exe', '',
    SW_HIDE, ewWaitUntilTerminated, ExitCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM data-watch-edge-collector.exe', '',
    SW_HIDE, ewWaitUntilTerminated, ExitCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM data-watch-uploader.exe', '',
    SW_HIDE, ewWaitUntilTerminated, ExitCode);
#endif
#if BuildRole == "distributed-center"
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM data-watch-ingest.exe', '',
    SW_HIDE, ewWaitUntilTerminated, ExitCode);
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  StopRoleService;
  StopOrphanedRoleProcesses;
  Result := '';
end;

procedure CopyIfAbsent(SourcePath, TargetPath: String);
begin
  if not FileExists(TargetPath) then
    CopyFile(SourcePath, TargetPath, False);
end;

procedure ProvisionRoleConfig;
var
  InstallPath: String;
  DataPath: String;
begin
  InstallPath := ExpandConstant('{app}');
  DataPath := ExpandConstant('{commonappdata}\{#MyDataDir}');
#if BuildRole == "centralized"
  CopyIfAbsent(InstallPath + '\templates\collector-settings.toml.example', DataPath + '\collector\config\settings.toml');
  CopyIfAbsent(InstallPath + '\templates\writer-settings.toml.example', DataPath + '\writer\config\settings.toml');
#endif
#if BuildRole == "edge"
  CopyIfAbsent(InstallPath + '\templates\collector-settings.toml.example', DataPath + '\collector\config\settings.toml');
#endif
#if BuildRole == "distributed-center"
  CopyIfAbsent(InstallPath + '\templates\writer-settings.toml.example', DataPath + '\writer\config\settings.toml');
#endif
end;

function SystemFunction036(var RandomBuffer: Byte; RandomBufferLength: Cardinal): Boolean;
  external 'SystemFunction036@advapi32.dll stdcall';

function EnableWindow(hWnd: HWND; bEnable: LongInt): LongInt;
  external 'EnableWindow@user32.dll stdcall';

function GenerateToken: String;
var
  Buffer: array[0..31] of Byte;
  HexDigits: String;
  MinByte: Byte;
  MaxByte: Byte;
  Index: Integer;
begin
  HexDigits := '0123456789abcdef';
  { 服务令牌必须是密码学安全随机值。原先用 Inno 的 Random()（非 CSPRNG，
    以时钟/计数器为种子，可预测）。RtlGenRandom(SystemFunction036) 自
    Windows XP 起随系统提供，无外部依赖，取 256 位。}
  if not SystemFunction036(Buffer[0], SizeOf(Buffer)) then
    RaiseException('Unable to obtain cryptographically secure random bytes for the service token');
  { 退化守卫：若缓冲区未被真正随机填充（例如按引用传参失败导致全零/常量），
    宁可让安装失败，也不写入可预测的服务令牌。真实 CSPRNG 输出的极差几乎总是 255。}
  MinByte := Buffer[0];
  MaxByte := Buffer[0];
  for Index := 1 to SizeOf(Buffer) - 1 do
  begin
    if Buffer[Index] < MinByte then MinByte := Buffer[Index];
    if Buffer[Index] > MaxByte then MaxByte := Buffer[Index];
  end;
  if (MaxByte - MinByte) < 16 then
    RaiseException('Refusing to use a degenerate service token (randomness check failed)');
  Result := '';
  for Index := 0 to SizeOf(Buffer) - 1 do
    Result := Result
      + HexDigits[(Buffer[Index] shr 4) + 1]
      + HexDigits[(Buffer[Index] and $0F) + 1];
end;

procedure HardenTokenFile(TokenPath: String);
var
  ResultCode: Integer;
begin
  { 服务令牌是明文秘密：只允许 管理员 + SYSTEM 读取/修改，且不继承上级。
    B50/S3：只靠 `/inheritance:r` + `/grant:r` 形成不了白名单——前者只删**继承**的 ACE，后者只
    替换**所列 SID** 的显式 ACE。隔离实验（icacls）已证实：文件上若已有**显式** `BUILTIN\Users:(RX)`，
    只跑 `/inheritance:r /grant:r 544/18` 之后它**仍然在**；补 `/remove:g` 才被移除。
    因此这里必须显式回收广域 SID：Users(S-1-5-32-545)、Everyone(S-1-1-0)、
    Authenticated Users(S-1-5-11)。}
  if not Exec(
      'icacls.exe',
      '"' + TokenPath + '" /inheritance:r /grant:r'
        + ' "*S-1-5-32-544:F"'
        + ' "*S-1-5-18:F"'
        + ' /remove:g "*S-1-5-32-545" "*S-1-1-0" "*S-1-5-11"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('Unable to run icacls for ' + TokenPath);
  if ResultCode <> 0 then
    RaiseException(Format('Unable to restrict permissions on %s (icacls exit %d)', [TokenPath, ResultCode]));
end;

{ 4 个明文秘密必须始终是「不继承上级」的显式白名单。
  恢复普通数据的继承权限时必须跳过它们，不能先开放再收紧。 }
procedure HardenDataSecrets(DataPath: String);
begin
  if FileExists(DataPath + '\config.toml') then HardenTokenFile(DataPath + '\config.toml');
  if FileExists(DataPath + '\.config.toml.tmp') then HardenTokenFile(DataPath + '\.config.toml.tmp');
  if FileExists(DataPath + '\agent_service_token.txt') then HardenTokenFile(DataPath + '\agent_service_token.txt');
  if FileExists(DataPath + '\edge\storage-ipc-tokens.json') then HardenTokenFile(DataPath + '\edge\storage-ipc-tokens.json');
end;

{ 递归扫描：任何重解析点（junction/symlink）都必须拒绝，且要在执行 /T 之前扫完。
  icacls 的 /T 会**穿过** junction 去改链接目标（隔离实验证实：root\link\secret.txt
  被列入处理清单），所以守卫必须先跑，否则安装器可能去改数据根之外的文件。 }
procedure AssertNoLinkedEntries(DataPath: String);
var
  FindRec: TFindRec;
  EntryPath: String;
begin
  if not FindFirst(DataPath + '\*', FindRec) then Exit;
  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        EntryPath := DataPath + '\' + FindRec.Name;
        if (FindRec.Attributes and $400) <> 0 then
          RaiseException('Refusing linked data entry ' + EntryPath);
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          AssertNoLinkedEntries(EntryPath);
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

procedure RestoreInheritedDataPermissions(DataPath, RootPath: String);
var
  FindRec: TFindRec;
  EntryPath: String;
  Params: String;
  IsEdgeDirectory: Boolean;
  ResultCode: Integer;
begin
  { 普通子树批量 icacls /T，避免逐文件启动进程造成数分钟停顿。
    根目录的三个秘密直接跳过；edge 目录只处理自身，再遍历直接子项，
    跳过 storage-ipc-tokens.json，其他子树仍批量处理。
    不能对数据根或含秘密的 edge 目录跑 /T，否则会重新打开秘密的继承。 }
  if not FindFirst(DataPath + '\*', FindRec) then Exit;
  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        EntryPath := DataPath + '\' + FindRec.Name;
        if (FindRec.Attributes and $400) <> 0 then
          RaiseException('Refusing linked data entry ' + EntryPath);
        if (CompareText(EntryPath, RootPath + '\config.toml') <> 0) and
           (CompareText(EntryPath, RootPath + '\.config.toml.tmp') <> 0) and
           (CompareText(EntryPath, RootPath + '\agent_service_token.txt') <> 0) and
           (CompareText(EntryPath, RootPath + '\edge\storage-ipc-tokens.json') <> 0) then
        begin
          AssertNoLinkedEntries(EntryPath);
          IsEdgeDirectory := (CompareText(EntryPath, RootPath + '\edge') = 0) and
            ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0);
          Params := '"' + EntryPath + '" /inheritance:e /C';
          if not IsEdgeDirectory then
            Params := Params + ' /T';
          if not Exec('icacls.exe', Params,
              '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
            RaiseException('Unable to restore inherited permissions for ' + EntryPath);
          if ResultCode <> 0 then
            RaiseException(Format('Unable to restore permissions on %s (icacls exit %d)', [EntryPath, ResultCode]));
          if IsEdgeDirectory then
            RestoreInheritedDataPermissions(EntryPath, RootPath);
        end;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

procedure HardenDataPermissions(DataPath: String);
var
  ResultCode: Integer;
begin
  { Protect management secrets before changing parent inheritance. }
  HardenDataSecrets(DataPath);
  { 显式落实 ACL：ProgramData 下的目录默认允许 Users 创建/追加，
    同一个用户还能对自己创建的对象拿到完全控制——对存放服务令牌与
    点位/配置的数据根来说过宽。
     做法：去掉继承并显式授予「管理员 + SYSTEM 完全控制」，另给 Users 只读执行
    （保留合法的读取面）；管理秘密先单独收紧，恢复继承之后必须再收紧一次。
     不使用 [Dirs] Permissions 关键字是为了能显式关闭继承并覆盖存量目录。}
  if not Exec(
      'icacls.exe',
      '"' + DataPath + '" /inheritance:r /grant:r'
        + ' "*S-1-5-32-544:(OI)(CI)F"'
        + ' "*S-1-5-18:(OI)(CI)F"'
        + ' "*S-1-5-32-545:(OI)(CI)RX"'
        + ' /C',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('Unable to run icacls for ' + DataPath);
  if ResultCode <> 0 then
    RaiseException(Format('Unable to restrict permissions on %s (icacls exit %d)', [DataPath, ResultCode]));
  { 根目录关闭继承；子项继承根目录的有效授权。不能递归 /inheritance:r：
    (OI)(CI) 授权在普通文件上不生效，会把仅有继承权限的配置清为空 DACL。 }
  RestoreInheritedDataPermissions(DataPath, DataPath);
  { 再核实秘密白名单；遍历过程中始终跳过秘密，失败时也不会开放其继承。 }
  HardenDataSecrets(DataPath);
end;

procedure RegisterRoleService;
var
  InstallPath: String;
  DataPath: String;
  AgentPath: String;
  TokenPath: String;
  IpcTokenPath: String;
  Token: String;
  Lines: TStringList;
begin
  InstallPath := ExpandConstant('{app}');
  DataPath := ExpandConstant('{commonappdata}\{#MyDataDir}');
  AgentPath := InstallPath + '\agent\{#MyAgentExe}';
  TokenPath := DataPath + '\agent_service_token.txt';
  Token := '';
  if FileExists(TokenPath) then
  begin
    Lines := TStringList.Create;
    try
      Lines.LoadFromFile(TokenPath);
      if Lines.Count > 0 then
        Token := Trim(Lines[0]);
    finally
      Lines.Free;
    end;
  end;
  if Token = '' then
  begin
    Token := GenerateToken;
    Lines := TStringList.Create;
    try
      Lines.Add(Token);
      Lines.SaveToFile(TokenPath);
    finally
      Lines.Free;
    end;
  end;

  { 令牌写好后立刻落实 ACL：先收紧数据根（存量目录一并处理），再单独锁住**两个**秘密文件——
    agent 服务令牌，以及 Edge Storage 的 IPC 令牌。后者此前从未被安装器加固：升级安装时它若已
    存在，只会在数据根递归加固中拿到继承来的 Users 只读，随后无人回收；运行期由
    `ensure_token_file` 新建的文件同样需要收敛（Python 侧已同步修复）。
    路径与 `edge/app/paths.py::edge_database_path()` 的父目录一致：<数据根>\edge\storage-ipc-tokens.json }
  HardenDataPermissions(DataPath);
  if FileExists(TokenPath) then
    HardenTokenFile(TokenPath);
  IpcTokenPath := DataPath + '\edge\storage-ipc-tokens.json';
  if FileExists(IpcTokenPath) then
    HardenTokenFile(IpcTokenPath);


  if not ServiceExists then
    if Nssm('install {#MyServiceName} "' + AgentPath + '"') <> 0 then
      RaiseException('Unable to install service {#MyServiceName}');
  if Nssm('set {#MyServiceName} Application "' + AgentPath + '"') <> 0 then
    RaiseException('Unable to set Agent executable');
  if Nssm('set {#MyServiceName} AppDirectory "' + InstallPath + '\agent"') <> 0 then
    RaiseException('Unable to set Agent working directory');
  if Nssm('set {#MyServiceName} AppEnvironmentExtra ' +
      '"DATAWATCH_ROLE={#BuildRole}" ' +
      '"DATAWATCH_AGENT_PORT={#MyAgentPort}" ' +
      '"DATAWATCH_INSTALL_ROOT=' + InstallPath + '" ' +
      '"DATAWATCH_DATA_ROOT=' + DataPath + '" ' +
      '"DATAWATCH_AGENT_SERVICE_TOKEN=' + Token + '"') <> 0 then
    RaiseException('Unable to set Agent environment');
  if Nssm('set {#MyServiceName} Start SERVICE_AUTO_START') <> 0 then
    RaiseException('Unable to set Agent service startup type');
  if Nssm('set {#MyServiceName} AppStopMethodConsole 60000') <> 0 then
    RaiseException('Unable to set Agent graceful stop timeout');
  if Nssm('set {#MyServiceName} AppStopMethodWindow 1500') <> 0 then
    RaiseException('Unable to set Agent window stop timeout');
  if Nssm('set {#MyServiceName} AppStopMethodThreads 1500') <> 0 then
    RaiseException('Unable to set Agent thread stop timeout');
  { nssm 用 AppThrottle 兼作两件事：应用「起够多久才算启动成功」的判定，以及
    `nssm start` 等 SERVICE_RUNNING 的超时——service.cpp 里 cutoff 直接取自
    NSSM_REG_THROTTLE，缺省 NSSM_RESET_THROTTLE_RESTART=1500ms，超时即返回 1。
    本 agent 启动要数秒（uvicorn + storage 子进程），1500ms 必然超时，于是
    安装器误报「Unable to start service」——而服务其实已启动。2026-10-08 三次安装
    实测：Edge / Distributed-Center 均误报，事后服务 Running、端口正常响应。 }
  if Nssm('set {#MyServiceName} AppThrottle 30000') <> 0 then
    RaiseException('Unable to set Agent start throttle');
  if Nssm('set {#MyServiceName} AppStdout "' + DataPath + '\runtime\logs\agent-service.out.log"') <> 0 then
    RaiseException('Unable to set Agent stdout log');
  if Nssm('set {#MyServiceName} AppStderr "' + DataPath + '\runtime\logs\agent-service.err.log"') <> 0 then
    RaiseException('Unable to set Agent stderr log');
  if Nssm('start {#MyServiceName}') <> 0 then
    RaiseException('Unable to start service {#MyServiceName}');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ProvisionRoleConfig;
    RegisterRoleService;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    { Exec disables the native window without changing the VCL Enabled property.
      Restore input only after installation and service registration have ended. }
    if EnableWindow(WizardForm.Handle, 1) <> 0 then
      Log('Restored disabled wizard window on the finished page.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: String;
begin
  if CurUninstallStep = usUninstall then
    RemoveRoleService
  else if CurUninstallStep = usPostUninstall then
  begin
    DataPath := ExpandConstant('{commonappdata}\{#MyDataDir}');
    if DirExists(DataPath) and
       (MsgBox('是否同时删除该角色的配置与运行数据？' + #13#10 + DataPath,
         mbConfirmation, MB_YESNO) = IDYES) then
      DelTree(DataPath, True, True, True);
  end;
end;
