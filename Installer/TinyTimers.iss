#define MyAppName "Tiny Timers"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppExeName "TinyTimers.exe"

[Setup]
AppId={{6C6F7E36-7A9B-4C6D-9C6E-2C7F6C6E9A21}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=TinyTimersSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\TinyTimers\publish\TinyTimers.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{app}\Uninstaller.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  AppUninstallGuid = '{6C6F7E36-7A9B-4C6D-9C6E-2C7F6C6E9A21}';

procedure CurStepChanged(CurStep: TSetupStep);
var
  OldExe, OldDat, NewExe, NewDat, UninstallKey: String;
  RootKey: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { Inno always writes its own uninstaller as unins000.exe; rename it to something friendlier
      and re-point the Start Menu shortcut and Control Panel entry that reference it. }
    OldExe := ExpandConstant('{app}\unins000.exe');
    OldDat := ExpandConstant('{app}\unins000.dat');
    NewExe := ExpandConstant('{app}\Uninstaller.exe');
    NewDat := ExpandConstant('{app}\Uninstaller.dat');

    if FileExists(OldExe) then
    begin
      if FileExists(NewExe) then
        DeleteFile(NewExe);
      RenameFile(OldExe, NewExe);

      if FileExists(OldDat) then
      begin
        if FileExists(NewDat) then
          DeleteFile(NewDat);
        RenameFile(OldDat, NewDat);
      end;

      if IsAdminInstallMode then
        RootKey := HKEY_LOCAL_MACHINE
      else
        RootKey := HKEY_CURRENT_USER;

      UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + AppUninstallGuid + '_is1';
      RegWriteStringValue(RootKey, UninstallKey, 'UninstallString', '"' + NewExe + '"');
      RegWriteStringValue(RootKey, UninstallKey, 'QuietUninstallString', '"' + NewExe + '" /VERYSILENT');
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir, UninstallKey, ManifestFile, FoldersFile, FilePath, Folder: String;
  RootKey: Integer;
  ManifestLines, FolderLines: TArrayOfString;
  i: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { Always remove the "run on startup" entry; a leftover pointing at a deleted exe helps no one }
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'TinyTimers');

    { Inno normally removes its own Add/Remove Programs entry automatically, but since we
      overwrite UninstallString/QuietUninstallString by hand at install time (see CurStepChanged),
      that auto-cleanup no longer reliably fires, so remove the key ourselves. }
    if IsAdminInstallMode then
      RootKey := HKEY_LOCAL_MACHINE
    else
      RootKey := HKEY_CURRENT_USER;

    UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + AppUninstallGuid + '_is1';
    RegDeleteKeyIncludingSubkeys(RootKey, UninstallKey);

    DataDir := ExpandConstant('{localappdata}\TinyTimers');
    if (not UninstallSilent) and DirExists(DataDir) then
    begin
      if MsgBox('Do you also want to remove all Tiny Timers data (saved timers, options, and any cached files)?'
        + #13#10#13#10 + DataDir + #13#10#13#10 + 'This cannot be undone.',
        mbConfirmation, MB_YESNO) = IDYES then
      begin
        { The app maintains a manifest of every timer file's exact current path, wherever the user
          has pointed the timer-files location (it doesn't have to be under DataDir). Delete exactly
          those files - not a wildcard sweep of the folder - so a custom location the user shares with
          other files (e.g. their Documents folder) doesn't lose anything that isn't ours. }
        ManifestFile := DataDir + '\file-manifest.txt';
        if LoadStringsFromFile(ManifestFile, ManifestLines) then
        begin
          for i := 0 to GetArrayLength(ManifestLines) - 1 do
          begin
            FilePath := Trim(ManifestLines[i]);
            if (FilePath <> '') and FileExists(FilePath) then
              DeleteFile(FilePath);
          end;
        end;

        { Also tidy up any custom folder the user ever pointed timer files at. We only ever
          remove the folder itself if it's completely empty afterward - never its contents by
          wildcard - since it could be a folder the user shares with unrelated files. }
        FoldersFile := DataDir + '\known-folders.txt';
        if LoadStringsFromFile(FoldersFile, FolderLines) then
        begin
          for i := 0 to GetArrayLength(FolderLines) - 1 do
          begin
            Folder := Trim(FolderLines[i]);
            if (Folder <> '') and DirExists(Folder) then
              RemoveDir(Folder);
          end;
        end;

        DelTree(DataDir, True, True, True);

        { The manifest file we just read can occasionally still be briefly locked, which
          leaves it (and so the folder) behind. Give it a moment and retry once. }
        if DirExists(DataDir) then
        begin
          Sleep(250);
          DelTree(DataDir, True, True, True);
        end;
      end;
    end;
  end;
end;
