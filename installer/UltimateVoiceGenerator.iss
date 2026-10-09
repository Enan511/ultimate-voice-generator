#ifndef PackageOutput
  #define PackageOutput "output"
#endif
[Setup]
AppId={{95B2600F-C743-4DB5-84AB-E64F998AC621}
AppName=Ultimate Voice Generator
AppVersion=3.2.1
AppPublisher=Ultimate Voice Generator
AppCopyright=Third-party notices are included with the application.
DefaultDirName={localappdata}\Programs\Ultimate Voice Generator
DefaultGroupName=Ultimate Voice Generator
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UsePreviousAppDir=no
UsePreviousTasks=no
Uninstallable=IsTaskSelected('uninstaller')
CreateUninstallRegKey=IsTaskSelected('uninstaller')
UninstallDisplayIcon={app}\Ultimate Voice Generator.exe
SetupIconFile=..\assets\ultimate_voice_generator.ico
AppMutex=Local\UltimateVoiceGenerator.Running
CloseApplications=no
RestartApplications=no
WizardStyle=modern
WizardSizePercent=110
OutputDir={#PackageOutput}
OutputBaseFilename=Ultimate-Voice-Generator-Setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=2
DiskSpanning=yes
DiskSliceSize=1500000000
SlicesPerDisk=1
UninstallFilesDir={app}\uninstall
SetupLogging=yes

[Tasks]
Name: uninstaller; Description: "Include an uninstaller (recommended)"; GroupDescription: "Installation options:"; Flags: checkedonce
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: startmenuicon; Description: "Create Start menu shortcuts"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
Source: "payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "tools\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{userdesktop}\Ultimate Voice Generator"; Filename: "{app}\Ultimate Voice Generator.exe"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; AppUserModelID: "UltimateVoiceGenerator.Desktop"; Tasks: desktopicon
Name: "{userprograms}\Ultimate Voice Generator\Ultimate Voice Generator"; Filename: "{app}\Ultimate Voice Generator.exe"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; AppUserModelID: "UltimateVoiceGenerator.Desktop"; Tasks: startmenuicon
Name: "{userprograms}\Ultimate Voice Generator\Uninstall Ultimate Voice Generator"; Filename: "{uninstallexe}"; Tasks: startmenuicon uninstaller

[Run]
Filename: "{app}\Ultimate Voice Generator.exe"; Description: "Launch Ultimate Voice Generator"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}\uninstall"
Type: dirifempty; Name: "{app}"

[Code]
const
  WebViewKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
var
  RemoveRecordings: Boolean;

function HasWebView: Boolean;
var V: String;
begin
  Result := ((RegQueryStringValue(HKLM32, WebViewKey, 'pv', V) or
             RegQueryStringValue(HKCU, WebViewKey, 'pv', V)) and (V <> '') and (V <> '0.0.0.0'));
end;

function DirectoryHasContents(const Dir: String): Boolean;
var Found: TFindRec;
begin
  Result := False;
  if FindFirst(AddBackslash(Dir) + '*', Found) then begin
    try
      repeat
        if (Found.Name <> '.') and (Found.Name <> '..') then begin Result := True; Break; end;
      until not FindNext(Found);
    finally FindClose(Found); end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer; Probe: String;
begin
  Result := '';
  if DirectoryHasContents(ExpandConstant('{app}')) then begin
    Result := 'Choose an empty folder. To replace an existing installation, uninstall it first. Your recordings can be kept.'; Exit;
  end;
  if not ForceDirectories(ExpandConstant('{app}')) then begin Result := 'Cannot create the destination. Choose a writable folder.'; Exit; end;
  Probe := ExpandConstant('{app}\uvg-install-write-test.tmp');
  if not SaveStringToFile(Probe, 'Ultimate Voice Generator installation check', False) then begin Result := 'The destination is not writable. Choose a folder under your user account.'; Exit; end;
  DeleteFile(Probe);
  if not HasWebView then begin
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Code) then begin
      Result := 'Could not start the Microsoft WebView2 installer. Install Microsoft Edge WebView2 Runtime and run Setup again.'; Exit;
    end;
    if not HasWebView then Result := 'Microsoft Edge WebView2 Runtime is required. Connect to the internet and retry, or install the runtime from Microsoft first.';
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  RemoveRecordings := ExpandConstant('{param:REMOVERECORDINGS|0}') = '1';
  if not UninstallSilent then
    RemoveRecordings := MsgBox('Also delete audio recordings created by Ultimate Voice Generator?' + #13#10#13#10 +
      'Yes: remove app-created recordings whose files are unchanged.' + #13#10 +
      'No: keep your recordings.' + #13#10#13#10 +
      'Settings and app cache are removed either way. Unrelated files and original reference recordings are preserved.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Code: Integer; Args: String;
begin
  if CurUninstallStep = usUninstall then begin
    Args := '--uninstall-cleanup';
    if RemoveRecordings then Args := Args + ' --remove-recordings';
    if FileExists(ExpandConstant('{app}\Ultimate Voice Generator.exe')) then begin
      if not Exec(ExpandConstant('{app}\Ultimate Voice Generator.exe'), Args, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
        Log('Could not start runtime data cleanup; existing user data was preserved.')
      else if Code <> 0 then Log('Some runtime files could not be removed and were preserved.');
    end;
  end;
end;
